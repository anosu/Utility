#include <dlfcn.h>
#include <jni.h>
#include <stdint.h>

static JavaVM *vm;
static jclass helper;
static jclass unity_player;
static jfieldID activity_field;

static JNIEnv *environment(int *attached) {
    *attached = 0;
    if (!vm) return NULL;
    JNIEnv *env = NULL;
    jint status = (*vm)->GetEnv(vm, (void **)&env, JNI_VERSION_1_6);
    if (status == JNI_OK) return env;
    if (status != JNI_EDETACHED || (*vm)->AttachCurrentThread(vm, &env, NULL) != JNI_OK)
        return NULL;
    *attached = 1;
    return env;
}

static void release_environment(int attached) {
    if (attached) (*vm)->DetachCurrentThread(vm);
}

static int clear_exception(JNIEnv *env) {
    if (!(*env)->ExceptionCheck(env)) return 0;
    (*env)->ExceptionDescribe(env);
    (*env)->ExceptionClear(env);
    return 1;
}

static jclass unity_class(JNIEnv *env) {
    if (unity_player) return unity_player;
    jclass unity = (*env)->FindClass(env, "com/unity3d/player/UnityPlayer");
    if (!unity) clear_exception(env);

    if (!unity) {
        jclass thread = (*env)->FindClass(env, "android/app/ActivityThread");
        if (!thread || clear_exception(env)) return NULL;
        jmethodID current = (*env)->GetStaticMethodID(env, thread, "currentApplication", "()Landroid/app/Application;");
        jobject application = current ? (*env)->CallStaticObjectMethod(env, thread, current) : NULL;
        (*env)->DeleteLocalRef(env, thread);
        if (!application || clear_exception(env)) return NULL;
        jclass application_class = (*env)->GetObjectClass(env, application);
        jmethodID get_loader = (*env)->GetMethodID(env, application_class, "getClassLoader", "()Ljava/lang/ClassLoader;");
        jobject loader = get_loader ? (*env)->CallObjectMethod(env, application, get_loader) : NULL;
        (*env)->DeleteLocalRef(env, application_class);
        (*env)->DeleteLocalRef(env, application);
        if (!loader || clear_exception(env)) return NULL;
        jclass loader_class = (*env)->GetObjectClass(env, loader);
        jmethodID load = (*env)->GetMethodID(env, loader_class, "loadClass", "(Ljava/lang/String;)Ljava/lang/Class;");
        jstring name = (*env)->NewStringUTF(env, "com.unity3d.player.UnityPlayer");
        unity = load ? (jclass)(*env)->CallObjectMethod(env, loader, load, name) : NULL;
        (*env)->DeleteLocalRef(env, name);
        (*env)->DeleteLocalRef(env, loader_class);
        (*env)->DeleteLocalRef(env, loader);
        if (clear_exception(env)) return NULL;
    }
    unity_player = (jclass)(*env)->NewGlobalRef(env, unity);
    (*env)->DeleteLocalRef(env, unity);
    return unity_player;
}

static jobject activity(JNIEnv *env) {
    jclass unity = unity_class(env);
    if (!unity) return NULL;
    if (!activity_field)
        activity_field = (*env)->GetStaticFieldID(env, unity, "currentActivity", "Landroid/app/Activity;");
    jobject result = activity_field ? (*env)->GetStaticObjectField(env, unity, activity_field) : NULL;
    if (clear_exception(env)) return NULL;
    return result;
}

static JavaVM *find_vm(void) {
    typedef jint (*get_vms)(JavaVM **, jsize, jsize *);
    get_vms find = (get_vms)dlsym(RTLD_DEFAULT, "JNI_GetCreatedJavaVMs");
    if (!find) {
        void *art = dlopen("libart.so", RTLD_NOW);
        if (art) find = (get_vms)dlsym(art, "JNI_GetCreatedJavaVMs");
    }
    JavaVM *found = NULL;
    jsize count = 0;
    return find && find(&found, 1, &count) == JNI_OK && count == 1 ? found : NULL;
}

static jclass load_helper(JNIEnv *env, jobject current_activity, const uint8_t *dex, int length) {
    jclass activity_class = (*env)->GetObjectClass(env, current_activity);
    jmethodID get_loader = (*env)->GetMethodID(env, activity_class, "getClassLoader", "()Ljava/lang/ClassLoader;");
    jobject parent = get_loader ? (*env)->CallObjectMethod(env, current_activity, get_loader) : NULL;
    (*env)->DeleteLocalRef(env, activity_class);
    if (!parent || clear_exception(env)) return NULL;

    jbyteArray bytes = (*env)->NewByteArray(env, length);
    if (bytes) (*env)->SetByteArrayRegion(env, bytes, 0, length, (const jbyte *)dex);
    jclass buffer_class = (*env)->FindClass(env, "java/nio/ByteBuffer");
    jmethodID wrap = buffer_class ? (*env)->GetStaticMethodID(env, buffer_class, "wrap", "([B)Ljava/nio/ByteBuffer;") : NULL;
    jobject buffer = wrap ? (*env)->CallStaticObjectMethod(env, buffer_class, wrap, bytes) : NULL;
    if (buffer_class) (*env)->DeleteLocalRef(env, buffer_class);
    if (bytes) (*env)->DeleteLocalRef(env, bytes);
    if (!buffer || clear_exception(env)) { (*env)->DeleteLocalRef(env, parent); return NULL; }

    jclass loader_class = (*env)->FindClass(env, "dalvik/system/InMemoryDexClassLoader");
    jmethodID constructor = loader_class ? (*env)->GetMethodID(env, loader_class, "<init>", "(Ljava/nio/ByteBuffer;Ljava/lang/ClassLoader;)V") : NULL;
    jobject loader = constructor ? (*env)->NewObject(env, loader_class, constructor, buffer, parent) : NULL;
    (*env)->DeleteLocalRef(env, buffer);
    (*env)->DeleteLocalRef(env, parent);
    if (!loader || clear_exception(env)) { if (loader_class) (*env)->DeleteLocalRef(env, loader_class); return NULL; }

    jmethodID load_class = (*env)->GetMethodID(env, loader_class, "loadClass", "(Ljava/lang/String;)Ljava/lang/Class;");
    jstring name = (*env)->NewStringUTF(env, "dev.jitsu.utility.toast.ToastOverlayBridge");
    jclass result = load_class ? (jclass)(*env)->CallObjectMethod(env, loader, load_class, name) : NULL;
    (*env)->DeleteLocalRef(env, name);
    (*env)->DeleteLocalRef(env, loader);
    (*env)->DeleteLocalRef(env, loader_class);
    if (clear_exception(env)) return NULL;
    return result;
}

__attribute__((visibility("default"))) int toast_start(void *java_vm, const uint8_t *dex, int length) {
    if (!dex || length <= 0) return 0;
    if (!vm) vm = java_vm ? (JavaVM *)java_vm : find_vm();
    int attached;
    JNIEnv *env = environment(&attached);
    if (!env) return 0;
    jobject current_activity = activity(env);
    if (!current_activity) { release_environment(attached); return 0; }
    if (!helper) {
        jclass local = load_helper(env, current_activity, dex, length);
        if (local) {
            helper = (jclass)(*env)->NewGlobalRef(env, local);
            (*env)->DeleteLocalRef(env, local);
        }
    }
    jmethodID start = helper ? (*env)->GetStaticMethodID(env, helper, "start", "(Landroid/app/Activity;)V") : NULL;
    if (start) (*env)->CallStaticVoidMethod(env, helper, start, current_activity);
    int failed = clear_exception(env);
    int ok = start && !failed;
    (*env)->DeleteLocalRef(env, current_activity);
    release_environment(attached);
    return ok;
}

__attribute__((visibility("default"))) int toast_present(const uint8_t *data, int length) {
    if (!helper || !data || length < 0) return 0;
    int attached;
    JNIEnv *env = environment(&attached);
    if (!env) return 0;
    jobject current_activity = activity(env);
    jbyteArray bytes = (*env)->NewByteArray(env, length);
    if (bytes) (*env)->SetByteArrayRegion(env, bytes, 0, length, (const jbyte *)data);
    jmethodID method = (*env)->GetStaticMethodID(env, helper, "present", "(Landroid/app/Activity;[B)V");
    if (current_activity && bytes && method)
        (*env)->CallStaticVoidMethod(env, helper, method, current_activity, bytes);
    int failed = clear_exception(env);
    int ok = current_activity && bytes && method && !failed;
    if (bytes) (*env)->DeleteLocalRef(env, bytes);
    if (current_activity) (*env)->DeleteLocalRef(env, current_activity);
    release_environment(attached);
    return ok;
}

__attribute__((visibility("default"))) int toast_viewport(int *values) {
    if (!helper || !values) return 0;
    int attached;
    JNIEnv *env = environment(&attached);
    if (!env) return 0;
    jmethodID method = (*env)->GetStaticMethodID(env, helper, "viewport", "()[I");
    jintArray array = method ? (jintArray)(*env)->CallStaticObjectMethod(env, helper, method) : NULL;
    int failed = clear_exception(env);
    int ok = array && !failed && (*env)->GetArrayLength(env, array) == 6;
    if (ok) (*env)->GetIntArrayRegion(env, array, 0, 6, (jint *)values);
    if (array) (*env)->DeleteLocalRef(env, array);
    release_environment(attached);
    return ok;
}

__attribute__((visibility("default"))) void toast_stop(void) {
    if (!helper && !unity_player) return;
    int attached;
    JNIEnv *env = environment(&attached);
    if (!env) return;
    if (helper) {
        jmethodID method = (*env)->GetStaticMethodID(env, helper, "stop", "()V");
        if (method) (*env)->CallStaticVoidMethod(env, helper, method);
        clear_exception(env);
        (*env)->DeleteGlobalRef(env, helper);
        helper = NULL;
    }
    if (unity_player) (*env)->DeleteGlobalRef(env, unity_player);
    unity_player = NULL;
    activity_field = NULL;
    release_environment(attached);
}
