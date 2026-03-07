let audio = null;
let audioUrl = null;
let _unlocked = false; // ✅ track state

export function initNotifySound(url) {
    audioUrl = url;
    try {
        audio = new Audio(url);
        audio.preload = "auto";
        audio.load(); // ✅ force load

        // تجهيز أوبجيكت الصوت للفك
        audio.muted = true;

        console.log("[notify] Sound initialized:", url);
    } catch (e) {
        console.error("[notify] Init failed:", e);
    }
}

// الدالة دي اللي هترتبط مباشرة بالكليك في index.html
export function unlockNotify() {
    if (!audio) {
        console.log("[notify] Not initialized");
        return false;
    }
    if (_unlocked) {
        return true;
    }
    try {
        // لازم يحصلوا ورا بعض بدون await عشان المتصفح يرضى بيهم كـ User Gesture
        audio.muted = true;
        audio.volume = 0;

        var playPromise = audio.play();
        if (playPromise !== undefined) {
            playPromise.then(() => {
                audio.pause();
                audio.currentTime = 0;
                audio.muted = false; // نرجع الصوت لطبيعته
                audio.volume = 1;
                _unlocked = true;
                console.log("[notify] ✅ Unlocked successfully via user gesture");
            }).catch(error => {
                console.log("[notify] ❌ Unlock failed:", error.message);
                _unlocked = false;
            });
        }
        return true;
    } catch (e) {
        console.log("[notify] ❌ Unlock exception:", e.message);
        _unlocked = false;
        return false;
    }
}

export async function playNotify() {
    if (!audio) {
        console.log("[notify] Not initialized");
        return false;
    }

    if (!_unlocked) {
        console.log("[notify] ❌ Cannot play - waiting for user interaction to unlock");
        return false;
    }

    try {
        audio.currentTime = 0;
        audio.volume = 1;
        audio.muted = false; // نتأكد إن الميوت مفكوك
        await audio.play();
        console.log("[notify] ✅ Played successfully");
        return true;
    } catch (e) {
        console.log("[notify] ❌ Play failed:", e.message);
        _unlocked = false; // لو فشل نرجع نقفله تاني
        return false;
    }
}

export async function resetNotify() {
    if (audio) {
        audio.pause();
        audio.currentTime = 0;
    }
    _unlocked = false;
    audio = null;
    if (audioUrl) {
        initNotifySound(audioUrl);
    }
}