/* Zafiyet Radarı — kayıt formu: şifre kurallarını yazarken canlı gösterir.
   Kurallar Security/PasswordPolicy.cs ile aynıdır; asıl karar sunucuda verilir.
   JS çalışmazsa form yine çalışır (gönder düğmesi sadece JS varken kilitlenir). */
(function () {
    "use strict";

    var rules = document.querySelector("[data-password-rules]");
    if (!rules) {
        return;
    }

    var form = rules.closest("form");
    var password = form.querySelector("[data-password-input]");
    var confirm = form.querySelector("[data-password-confirm]");
    var email = form.querySelector("[data-email-input]");
    var submit = form.querySelector("[data-register-submit]");
    var meter = rules.querySelector("[data-password-meter]");
    var countLabel = rules.querySelector("[data-category-count]");
    var group = rules.querySelector("[data-rule-group]");

    var minLength = parseInt(rules.dataset.minLength, 10) || 12;
    var requiredCategories = parseInt(rules.dataset.requiredCategories, 10) || 3;
    var emailFragment = parseInt(rules.dataset.emailFragment, 10) || 4;

    // Unicode kategorileri: Ç, Ğ, İ, Ö, Ş, Ü de harf sayılır (sunucudaki char.IsUpper/IsLower ile aynı).
    var categories = {
        upper: /\p{Lu}/u,
        lower: /\p{Ll}/u,
        digit: /\p{Nd}/u,
        symbol: /[^\p{L}\p{Nd}]/u
    };

    // Her kural satırına ekran okuyucular için gizli bir durum metni eklenir.
    var items = {};
    rules.closest("form").querySelectorAll("[data-rule]").forEach(function (li) {
        var state = document.createElement("span");
        state.className = "visually-hidden";
        li.appendChild(state);
        items[li.dataset.rule] = { li: li, state: state };
    });

    function mark(rule, met) {
        var item = items[rule];
        if (!item) {
            return;
        }
        item.li.classList.toggle("is-met", met);
        item.state.textContent = met ? " (sağlandı)" : " (sağlanmadı)";
    }

    function emailName() {
        var value = (email && email.value || "").trim();
        var at = value.indexOf("@");
        return (at >= 0 ? value.slice(0, at) : value).trim().toLowerCase();
    }

    function update() {
        var value = password.value;
        var hasValue = value.length > 0;

        var lengthOk = value.length >= minLength;
        mark("length", lengthOk);

        var count = 0;
        Object.keys(categories).forEach(function (name) {
            var met = categories[name].test(value);
            if (met) {
                count += 1;
            }
            mark(name, met);
        });
        var categoriesOk = count >= requiredCategories;
        countLabel.textContent = count + "/4";
        group.classList.toggle("is-met", categoriesOk);

        var name = emailName();
        var emailOk = hasValue && !(name.length >= emailFragment && value.toLowerCase().indexOf(name) !== -1);
        mark("email", emailOk);

        var matchOk = confirm.value.length > 0 && confirm.value === value;
        mark("match", matchOk);

        var valid = lengthOk && categoriesOk && emailOk && matchOk;
        submit.disabled = !valid;

        // Güç çubuğu: 0 boş, 1 zayıf, 2 orta, 3 yeterli, 4 güçlü.
        var level = 0;
        if (hasValue) {
            level = 1;
            if (lengthOk || count >= 2) { level = 2; }
            if (lengthOk && categoriesOk && emailOk) { level = 3; }
            if (level === 3 && count === 4 && value.length >= 16) { level = 4; }
        }
        meter.dataset.level = String(level);
    }

    password.addEventListener("input", update);
    confirm.addEventListener("input", update);
    if (email) {
        email.addEventListener("input", update);
    }

    update();
})();
