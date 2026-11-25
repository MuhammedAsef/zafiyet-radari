/* Zafiyet Radarı — Hangfire paneli grafik renkleri.
   hangfire.js'ten sonra yüklenir; jQuery hazır olay sırası sayesinde Hangfire.page
   oluşturulduktan sonra çalışır. Renkler hangfire-theme.css'teki tokenlarla aynıdır. */
$(function () {
    "use strict";

    var palette = {
        failed: [255, 90, 106],
        deleted: [124, 139, 166],
        succeeded: [52, 224, 180],
        grid: "rgba(138, 155, 180, 0.14)",
        tick: "#8a9bb4"
    };

    function rgba(rgb, alpha) {
        return "rgba(" + rgb[0] + ", " + rgb[1] + ", " + rgb[2] + ", " + alpha + ")";
    }

    function retheme(graph) {
        if (!graph || !graph._chart) {
            return;
        }

        var chart = graph._chart;
        var datasets = chart.data.datasets;
        var series = [palette.failed, palette.deleted, palette.succeeded];

        for (var i = 0; i < datasets.length && i < series.length; i++) {
            datasets[i].borderColor = rgba(series[i], 1);
            datasets[i].backgroundColor = rgba(series[i], i === 2 ? 0.18 : 0.12);
            datasets[i].borderWidth = 2;
        }

        ["xAxes", "yAxes"].forEach(function (key) {
            (chart.options.scales[key] || []).forEach(function (axis) {
                axis.gridLines = axis.gridLines || {};
                axis.gridLines.color = palette.grid;
                axis.gridLines.zeroLineColor = palette.grid;
                axis.ticks = axis.ticks || {};
                axis.ticks.fontColor = palette.tick;
            });
        });

        chart.update();
    }

    var page = window.Hangfire && window.Hangfire.page;
    if (page) {
        retheme(page.realtimeGraph);
        retheme(page.historyGraph);
    }
});
