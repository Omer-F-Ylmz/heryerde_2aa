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

// Süzgeç paneli: seçim değişince sonuç sayısı sayfa yenilenmeden sorulur (sayim=1, düz metin); gönderim yine formun
// kendisi, JS'siz de çalışır. ::details-content desteklemeyen tarayıcıda geniş ekranda panel açık başlar.
(function () {
  var form = document.querySelector("[data-filter-form]");
  if (!form) { return; }

  var details = form.closest("[data-filters]");
  if (details && !(window.CSS && CSS.supports("selector(::details-content)")) && window.matchMedia("(min-width: 1024px)").matches) {
    details.open = true;
  }

  var button = form.querySelector("[data-filter-count]");
  var status = form.querySelector("[data-filter-status]");
  var pending = null;

  form.addEventListener("change", function () {
    var params = new URLSearchParams(new FormData(form));
    params.set("sayim", "1");
    if (pending) { pending.abort(); }
    pending = new AbortController();

    fetch(form.getAttribute("action") + "?" + params.toString(), { signal: pending.signal })
      .then(function (response) { return response.ok ? response.text() : ""; })
      .then(function (text) {
        if (!/^\d+$/.test(text)) { return; }
        button.textContent = text === "0" ? "Uyan ürün yok" : text + " ürünü göster";
        status.textContent = text + " ürün bulundu";
      })
      .catch(function () {});
  });
})();

// Arama önerileri: başlıktaki kutuya yazılınca 200 ms durulunca /ara/oner sorulur; aşağı/yukarı ok önerilerde gezer, Esc
// kapatıp kutuya döner. JS yoksa kutu düz GET formudur.
(function () {
  var input = document.querySelector("[data-suggest]");
  if (!input || !window.fetch) { return; }

  var form = input.form;
  var panel = document.createElement("div");
  panel.className = "suggest";
  panel.id = "search-suggest";
  panel.hidden = true;
  var status = document.createElement("p");
  status.className = "visually-hidden";
  status.setAttribute("role", "status");
  form.appendChild(panel);
  form.appendChild(status);
  input.setAttribute("autocomplete", "off");
  input.setAttribute("aria-controls", panel.id);
  input.setAttribute("aria-expanded", "false");

  var timer = null;
  var pending = null;
  var items = function () { return Array.prototype.slice.call(panel.querySelectorAll(".suggest__item")); };
  var show = function (open) {
    panel.hidden = !open;
    input.setAttribute("aria-expanded", open ? "true" : "false");
  };

  input.addEventListener("input", function () {
    clearTimeout(timer);
    if (pending) { pending.abort(); }
    var term = input.value.trim();
    if (term.length < 2) { show(false); return; }

    timer = setTimeout(function () {
      pending = new AbortController();
      fetch("/ara/oner?q=" + encodeURIComponent(term), { signal: pending.signal })
        .then(function (response) { return response.ok ? response.text() : ""; })
        .then(function (html) {
          panel.innerHTML = html;
          var count = items().length;
          status.textContent = count > 0 ? count + " öneri" : "";
          show(count > 0);
        })
        .catch(function () {});
    }, 200);
  });

  form.addEventListener("keydown", function (event) {
    var list = items();
    if (panel.hidden || list.length === 0) { return; }

    if (event.key === "Escape") {
      show(false);
      input.focus();
      return;
    }

    if (event.key !== "ArrowDown" && event.key !== "ArrowUp") { return; }
    event.preventDefault();
    var next = list.indexOf(document.activeElement) + (event.key === "ArrowDown" ? 1 : -1);
    if (next < 0) { input.focus(); } else { list[Math.min(next, list.length - 1)].focus(); }
  });

  // Tıklama odağı kutuda tutar (Safari bağlantıya odak vermez, yoksa panel tıklamadan önce kapanırdı); odak formdan çıkınca kapanır.
  panel.addEventListener("mousedown", function (event) { event.preventDefault(); });
  form.addEventListener("focusout", function (event) {
    if (!form.contains(event.relatedTarget)) { show(false); }
  });
})();
