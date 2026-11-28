// Mobil tablo görünümü için sütun başlıklarını hücrelere data-label olarak kopyalar.
// Stil site.css'teki .table-stack kurallarında; 768px üstünde tablolar değişmeden kalır.
(function () {
  'use strict';

  function label(table) {
    var heads = Array.prototype.map.call(
      table.querySelectorAll(':scope > thead > tr:first-child > th'),
      function (th) { return th.textContent.trim(); });
    if (heads.length === 0) return;

    table.querySelectorAll(':scope > tbody > tr').forEach(function (row) {
      var col = 0;
      Array.prototype.forEach.call(row.children, function (cell) {
        var span = cell.colSpan || 1;
        if (span === 1 && heads[col] && !cell.hasAttribute('data-label')) {
          cell.setAttribute('data-label', heads[col]);
        }
        col += span;
      });
    });
  }

  function init() {
    document.querySelectorAll('table.table-stack').forEach(label);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
