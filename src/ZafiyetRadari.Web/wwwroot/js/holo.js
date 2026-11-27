// Giriş panelindeki 3B radarın imlece göre hafifçe eğilmesi.
// Yalnızca iki CSS değişkeni yazar (--holo-px, --holo-py, -1..1); yumuşatma CSS geçişinde.
// Hareket azaltma tercihinde hiç bağlanmaz.
(() => {
  "use strict";

  const holo = document.querySelector("[data-holo]");
  if (!holo || window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
    return;
  }

  let frame = 0;
  let x = 0;
  let y = 0;

  const apply = () => {
    frame = 0;
    holo.style.setProperty("--holo-px", x.toFixed(3));
    holo.style.setProperty("--holo-py", y.toFixed(3));
  };

  const schedule = () => {
    if (!frame) {
      frame = window.requestAnimationFrame(apply);
    }
  };

  window.addEventListener("pointermove", (event) => {
    x = (event.clientX / window.innerWidth) * 2 - 1;
    y = (event.clientY / window.innerHeight) * 2 - 1;
    schedule();
  }, { passive: true });

  document.documentElement.addEventListener("pointerleave", () => {
    x = 0;
    y = 0;
    schedule();
  });
})();
