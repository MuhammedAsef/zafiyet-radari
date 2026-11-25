// Dashboard grafikleri. Veri sayfadaki <script type="application/json"> bloğundan okunur (inline script yok, CSP uyumlu).
(function () {
    'use strict';

    var element = document.getElementById('dashboard-data');
    if (!element || typeof Chart === 'undefined') {
        return;
    }

    var data = JSON.parse(element.textContent);
    // Renkler temadan (site.css) okunur: tek yerden yönetilir.
    var styles = getComputedStyle(document.documentElement);
    function token(name, fallback) { return styles.getPropertyValue(name).trim() || fallback; }

    var colors = {
        text: token('--zr-muted', '#8a9bb4'),
        grid: token('--zr-line', '#1d2c45'),
        now: token('--zr-now', '#ff5a6a'),
        week: token('--zr-week', '#f4b740'),
        plan: token('--zr-plan', '#4fa3ff'),
        watch: token('--zr-watch', '#7c8ba6'),
        fixed: token('--zr-fixed', '#3ccf7e'),
        panel: token('--zr-panel', '#0e1829')
    };

    Chart.defaults.color = colors.text;
    Chart.defaults.borderColor = colors.grid;
    Chart.defaults.font.family = token('--zr-body', 'system-ui');
    Chart.defaults.maintainAspectRatio = false;

    var trend = document.getElementById('trendChart');
    if (trend) {
        new Chart(trend, {
            type: 'bar',
            data: {
                labels: data.trend.labels,
                datasets: [
                    { label: 'Açılan', data: data.trend.opened, backgroundColor: colors.now, borderRadius: 3, maxBarThickness: 14 },
                    { label: 'Kapanan', data: data.trend.resolved, backgroundColor: colors.fixed, borderRadius: 3, maxBarThickness: 14 }
                ]
            },
            options: {
                scales: {
                    x: { grid: { display: false }, ticks: { maxRotation: 0, autoSkipPadding: 12 } },
                    y: { beginAtZero: true, ticks: { precision: 0 }, border: { display: false } }
                },
                plugins: { legend: { position: 'bottom', labels: { boxWidth: 10, boxHeight: 10, usePointStyle: true } } }
            }
        });
    }

    var priority = document.getElementById('priorityChart');
    if (priority) {
        var total = data.priorities.values.reduce(function (a, b) { return a + b; }, 0);
        new Chart(priority, {
            type: 'doughnut',
            data: {
                labels: data.priorities.labels,
                datasets: [{
                    data: total > 0 ? data.priorities.values : [1],
                    backgroundColor: total > 0 ? [colors.now, colors.week, colors.plan, colors.watch] : [colors.grid],
                    borderColor: colors.panel,
                    borderWidth: 3
                }]
            },
            options: {
                cutout: '68%',
                plugins: {
                    legend: { position: 'bottom', display: total > 0, labels: { boxWidth: 10, boxHeight: 10, usePointStyle: true } },
                    tooltip: { enabled: total > 0 }
                }
            }
        });
    }
})();
