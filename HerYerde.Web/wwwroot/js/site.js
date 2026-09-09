// Countdown: data-countdown="ISO"; süre bitince öğe gizlenir.
document.querySelectorAll("[data-countdown]").forEach(function (el) {
  var end = new Date(el.getAttribute("data-countdown")).getTime();
  var cell = {
    d: el.querySelector("[data-part=d]"),
    h: el.querySelector("[data-part=h]"),
    m: el.querySelector("[data-part=m]"),
    s: el.querySelector("[data-part=s]")
  };
  var pad = function (n) { return n < 10 ? "0" + n : String(n); };
  var tick = function () {
    var left = Math.floor((end - Date.now()) / 1000);
    if (left <= 0) { el.hidden = true; return; }
    cell.d.textContent = Math.floor(left / 86400);
    cell.h.textContent = pad(Math.floor(left / 3600) % 24);
    cell.m.textContent = pad(Math.floor(left / 60) % 60);
    cell.s.textContent = pad(left % 60);
    setTimeout(tick, 1000);
  };
  tick();
});

// Alt kategori sekmeleri: seçili sekme yatay kaydırmada görünür alana gelir.
document.querySelectorAll(".tabs--scroll [aria-current=page]").forEach(function (el) {
  el.scrollIntoView({ block: "nearest", inline: "center" });
});
