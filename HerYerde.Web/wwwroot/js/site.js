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

// Sepete ekle: Giyim'de beden+renk seçimi bir varyanta denk gelene kadar buton kapalı.
(function () {
  var form = document.querySelector("[data-add-to-cart]");
  if (!form) { return; }

  var options = JSON.parse(form.getAttribute("data-variants") || "[]");
  if (options.length === 0) { return; }

  var field = form.querySelector("[data-variant-id]");
  var button = form.querySelector("[data-add-button]");
  var hint = document.querySelector("[data-add-hint]");
  var needsSize = options.some(function (o) { return o.Size; });
  var needsColor = options.some(function (o) { return o.Color; });

  var picked = function (name) {
    var input = document.querySelector("input[name=" + name + "]:checked");
    return input ? input.value : null;
  };

  var sync = function () {
    var size = picked("size");
    var color = picked("color");
    var match = options.filter(function (o) {
      return (!needsSize || o.Size === size) && (!needsColor || o.Color === color);
    })[0];

    var ok = Boolean(match) && match.Stock > 0;
    field.value = match ? match.Id : "";
    button.disabled = !ok;
    if (hint) {
      hint.textContent = ok
        ? "Stokta " + match.Stock + " adet var."
        : (match ? "Bu seçim tükendi." : "Sepete eklemek için beden ve renk seçin.");
    }
  };

  document.querySelectorAll("input[name=size], input[name=color]").forEach(function (input) {
    input.addEventListener("change", sync);
  });
  sync();
})();

// Ödeme yöntemi: IBAN kutusu yalnız havale seçiliyken görünür.
(function () {
  var box = document.querySelector("[data-pay] [data-iban]");
  if (!box) { return; }

  var sync = function () {
    var havale = document.getElementById("pay-havale");
    box.hidden = !(havale && havale.checked);
  };

  document.querySelectorAll("[data-pay] input[name=PaymentMethod]").forEach(function (input) {
    input.addEventListener("change", sync);
  });
  sync();
})();

// IBAN kopyala: pano yoksa metin seçilir.
document.querySelectorAll("[data-iban-copy]").forEach(function (button) {
  button.addEventListener("click", function () {
    var box = button.closest("[data-iban], .iban");
    var value = box.querySelector("[data-iban-value]");
    var done = box.querySelector("[data-iban-done]");
    var text = value.textContent.trim();

    var shown = function () {
      if (!done) { return; }
      done.hidden = false;
      setTimeout(function () { done.hidden = true; }, 2000);
    };

    if (navigator.clipboard) {
      navigator.clipboard.writeText(text).then(shown, function () {});
      return;
    }

    var range = document.createRange();
    range.selectNodeContents(value);
    var selection = window.getSelection();
    selection.removeAllRanges();
    selection.addRange(range);
    shown();
  });
});
