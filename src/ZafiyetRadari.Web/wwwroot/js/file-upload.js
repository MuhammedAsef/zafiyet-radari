/* Zafiyet Radarı — envanter dosyası yükleme alanı.
   Seçilen dosyanın adını ve boyutunu gösterir, uzantı/boyutu gönderimden önce kontrol eder,
   yükleme sürerken düğmeyi kilitler. Kontroller sunucuda da aynen yapılır. */
(function () {
    "use strict";

    function formatSize(bytes) {
        if (bytes < 1024) {
            return bytes + " B";
        }
        var kb = bytes / 1024;
        if (kb < 1024) {
            return kb.toFixed(1).replace(".", ",") + " KB";
        }
        return (kb / 1024).toFixed(1).replace(".", ",") + " MB";
    }

    document.querySelectorAll("[data-file-upload]").forEach(function (form) {
        var input = form.querySelector("[data-file-input]");
        var drop = form.querySelector("[data-file-drop]");
        var title = form.querySelector("[data-file-name]");
        var meta = form.querySelector("[data-file-meta]");
        var error = form.querySelector("[data-file-error]");
        var submit = form.querySelector("[data-file-submit]");
        var maxBytes = parseInt(form.dataset.maxBytes, 10) || 0;

        var emptyTitle = title.textContent;
        var emptyMeta = meta.textContent;
        var submitText = submit.textContent;

        function reset() {
            submit.textContent = submitText;
            drop.classList.remove("is-busy", "is-dragging");
            update();
        }

        function update() {
            var file = input.files && input.files[0];
            var problem = "";

            if (!file) {
                title.textContent = emptyTitle;
                meta.textContent = emptyMeta;
                drop.classList.remove("has-file", "is-invalid");
                error.textContent = "";
                submit.disabled = true;
                return;
            }

            if (!/\.json$/i.test(file.name)) {
                problem = "Sadece .json dosyaları yüklenebilir.";
            } else if (maxBytes > 0 && file.size > maxBytes) {
                problem = "Dosya en fazla " + formatSize(maxBytes) + " olabilir.";
            } else if (file.size === 0) {
                problem = "Dosya boş.";
            }

            title.textContent = file.name;
            meta.textContent = formatSize(file.size);
            drop.classList.add("has-file");
            drop.classList.toggle("is-invalid", problem !== "");
            error.textContent = problem;
            submit.disabled = problem !== "";
        }

        input.addEventListener("change", update);

        ["dragenter", "dragover"].forEach(function (type) {
            drop.addEventListener(type, function () { drop.classList.add("is-dragging"); });
        });
        ["dragleave", "drop"].forEach(function (type) {
            drop.addEventListener(type, function () { drop.classList.remove("is-dragging"); });
        });

        form.addEventListener("submit", function (event) {
            if (submit.disabled) {
                event.preventDefault();
                return;
            }
            submit.disabled = true;
            submit.textContent = "Yükleniyor…";
            drop.classList.add("is-busy");
        });

        // Geri tuşuyla sayfaya dönülürse (tarayıcı önbelleği) düğme "Yükleniyor…" durumunda kalmasın.
        window.addEventListener("pageshow", reset);

        update();
    });
})();
