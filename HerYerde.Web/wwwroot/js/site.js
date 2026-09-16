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

// Sepete ekle: Giyim'de beden+renk (örtüde yalnız renk) seçimi bir varyanta denk gelene kadar buton kapalı.
(function () {
  var form = document.querySelector("[data-add-to-cart]");
  if (!form) { return; }

  var options = JSON.parse(form.getAttribute("data-variants") || "[]");
  if (options.length === 0) { return; }

  var field = form.querySelector("[data-variant-id]");
  var button = form.querySelector("[data-add-button]");
  var hint = document.querySelector("[data-add-hint]");
  var pickText = hint ? hint.getAttribute("data-pick-text") : "";
  var lowStock = document.querySelector("[data-low-stock][data-low-stock-at]");
  var lowStockAt = lowStock ? Number(lowStock.getAttribute("data-low-stock-at")) : 0;
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
        : (match ? "Bu seçim tükendi." : pickText);
    }
    // "Son N adet" rozeti seçili varyantın stoğunu izler; eşik üstünde ya da seçim yokken gizli.
    if (lowStock) {
      var low = ok && match.Stock <= lowStockAt;
      lowStock.textContent = low ? "Son " + match.Stock + " adet" : "";
      lowStock.hidden = !low;
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

// Ödeme yöntemi: kart alanları yalnız kart seçiliyken görünür.
(function () {
  var box = document.querySelector("[data-pay] [data-card]");
  if (!box) { return; }

  var sync = function () {
    var kart = document.getElementById("pay-kart");
    box.hidden = !(kart && kart.checked);
  };

  document.querySelectorAll("[data-pay] input[name=PaymentMethod]").forEach(function (input) {
    input.addEventListener("change", sync);
  });
  sync();
})();

// Duyuru şeridi: çerezsiz kapatma. Kapatma yalnız bu oturum boyunca (sessionStorage) hatırlanır;
// depolama kapalıysa şerit her sayfada görünür, sayfa yine çalışır.
(function () {
  var strip = document.querySelector("[data-announcement]");
  if (!strip) { return; }

  var key = "heryerde.duyuru." + strip.getAttribute("data-announcement");
  var store = null;
  try { store = window.sessionStorage; } catch (error) { store = null; }

  if (store && store.getItem(key) === "1") {
    strip.remove();
    return;
  }

  var close = strip.querySelector("[data-announcement-close]");
  if (!close) { return; }

  close.hidden = false;
  close.addEventListener("click", function () {
    strip.remove();
    try { if (store) { store.setItem(key, "1"); } } catch (error) { /* depolama yoksa bir sonraki sayfada yine görünür */ }
  });
})();

// İl/ilçe: JS varsa "İlçeleri getir" düğmesi gizlenir, il değişince sayfa kendiliğinden tazelenir.
(function () {
  var province = document.querySelector("[data-address] [data-province]");
  var refresh = document.querySelector("[data-district-refresh]");
  if (!province || !refresh) { return; }

  // Gizlemek yetmez, devre dışı da bırakılır: bu düğme formun ilk submit'i olduğu için varsayılan
  // düğmedir ve bir alanda Enter'a basmak siparişi göndermek yerine ilçe listesini tazelerdi.
  // Devre dışı düğme varsayılan sayılmaz; tazeleme için kısa süreliğine açılır.
  refresh.hidden = true;
  refresh.disabled = true;

  // Klavyeyle gezinirken (ok tuşu her seçenekte "change" doğurur) sayfa her adımda yenilenmesin:
  // seçim durulunca tazelenir.
  var timer = null;
  province.addEventListener("change", function () {
    clearTimeout(timer);
    timer = setTimeout(function () {
      refresh.disabled = false;
      refresh.click();
    }, 500);
  });
})();

// Taksit tablosu: kart numarasının ilk 6 hanesi girilince bankanın tablosu istenir.
(function () {
  var slot = document.querySelector("[data-installments]");
  var number = document.getElementById("card-number");
  if (!slot || !number) { return; }

  var shown = "";
  number.addEventListener("input", function () {
    var bin = number.value.replace(/\D/g, "").slice(0, 6);
    if (bin.length < 6) {
      slot.innerHTML = "";
      shown = "";
      return;
    }

    if (bin === shown) { return; }
    shown = bin;
    fetch("/odeme/taksit?bin=" + bin, { headers: { "Accept": "text/html" } })
      .then(function (response) { return response.ok ? response.text() : ""; })
      .then(function (html) {
        if (shown === bin) { slot.innerHTML = html; }
      })
      .catch(function () {});
  });
})();

// 3D Secure: bankaya giden form sayfa açılınca kendiliğinden gönderilir.
document.querySelectorAll("form[data-autosubmit]").forEach(function (form) {
  form.submit();
});

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

// Kart önizlemesi: üzerine gelince ürün videosunun 3 saniyelik sessiz webm'i oynar, ayrılınca görsel geri gelir.
// Azaltılmış hareket tercihinde oynatıcı hiç kurulmaz; dokunmatikte ilk dokunuş da başlatır.
(function () {
  if (window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches) { return; }

  document.querySelectorAll("[data-onizleme]").forEach(function (marker) {
    var source = marker.getAttribute("data-onizleme");
    var card = marker.closest(".card");
    var media = card && card.querySelector(".card__media");
    if (!source || !media) { return; }

    var video = null;
    var start = function () {
      if (!video) {
        video = document.createElement("video");
        video.className = "card__preview";
        video.src = source;
        video.muted = true;
        video.loop = true;
        video.playsInline = true;
        video.setAttribute("aria-hidden", "true");
        media.appendChild(video);
      }

      video.currentTime = 0;
      var playing = video.play();
      if (playing && playing.catch) { playing.catch(function () {}); }
      media.classList.add("card__media--playing");
    };

    var stop = function () {
      if (!video) { return; }
      video.pause();
      media.classList.remove("card__media--playing");
    };

    // Başlık bağlantısı kartın tamamını kaplar (::after): imleç medyaya hiç "girmez", olay kartta dinlenir.
    card.addEventListener("pointerenter", start);
    card.addEventListener("pointerleave", stop);
  });
})();
