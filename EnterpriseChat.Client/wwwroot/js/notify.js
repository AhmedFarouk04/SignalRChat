let audio = null;

export function initNotifySound(url) {
  audio = new Audio(url);
  audio.preload = "auto";
}

export async function unlockNotify() {
  if (!audio) return false;
  try {
    audio.muted = true;
    await audio.play();
    audio.pause();
    audio.currentTime = 0;
    audio.muted = false;
    return true;
  } catch (e) {
    console.log("[notify] unlock blocked", e);
    return false;
  }
}

export async function playNotify() {
  if (!audio) return false;
  try {
    audio.currentTime = 0;
    await audio.play();
    return true;
  } catch (e) {
    console.log("[notify] play blocked", e);
    return false;
  }
}
