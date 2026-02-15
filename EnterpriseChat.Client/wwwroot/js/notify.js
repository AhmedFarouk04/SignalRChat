let audio = null;
let audioUrl = null;

export function initNotifySound(url) {
  audioUrl = url;
  try {
    audio = new Audio(url);
    audio.preload = "auto";
    console.log("[notify] Sound initialized with URL:", url);
  } catch (e) {
    console.error("[notify] Failed to create audio:", e);
  }
}

export async function resetNotify() {
  if (audio) {
    audio.pause();
    audio.currentTime = 0;
  }
  audio = null;
  
  if (audioUrl) {
    try {
      audio = new Audio(audioUrl);
      audio.preload = "auto";
    } catch (e) {
      console.error("[notify] Failed to recreate audio:", e);
    }
  }
}

export async function unlockNotify() {
  if (!audio) {
    console.log("[notify] Audio not initialized");
    return false;
  }
  
  try {
    audio.muted = true;
    await audio.play();
    audio.pause();
    audio.currentTime = 0;
    audio.muted = false;
    console.log("[notify] Sound unlocked successfully");
    return true;
  } catch (e) {
    console.log("[notify] Unlock blocked:", e);
    return false;
  }
}

export async function playNotify() {
  if (!audio) {
    console.log("[notify] Audio not initialized, trying to reinitialize");
    return false;
  }
  
  try {
    audio.currentTime = 0;
    await audio.play();
    console.log("[notify] Sound played successfully");
    return true;
  } catch (e) {
    console.log("[notify] Play blocked:", e);
    
    // Try to unlock and play again
    try {
      audio.muted = true;
      await audio.play();
      audio.pause();
      audio.currentTime = 0;
      audio.muted = false;
      
      await audio.play();
      console.log("[notify] Sound played after unlock");
      return true;
    } catch (retryError) {
      console.log("[notify] Retry failed:", retryError);
      return false;
    }
  }
}