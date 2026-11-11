// PIMLR #ANL: WebGL helpers for AnalyticsWriter.
//  - SyncFs: push persistentDataPath (IndexedDB) to disk after each flush, so closing the tab keeps the data.
//  - Download: "Download my session data" saves the newest .jsonl through the browser.
mergeInto(LibraryManager.library, {
  PimlrAnalytics_SyncFs: function () {
    if (typeof FS !== 'undefined' && FS.syncfs) {
      FS.syncfs(false, function (err) { if (err) console.warn('PIMLR analytics syncfs failed', err); });
    }
  },
  PimlrAnalytics_Download: function (namePtr, textPtr) {
    var name = UTF8ToString(namePtr);
    var text = UTF8ToString(textPtr);
    var blob = new Blob([text], { type: 'application/x-ndjson' });
    var a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = name;
    document.body.appendChild(a);
    a.click();
    setTimeout(function () { URL.revokeObjectURL(a.href); a.remove(); }, 1000);
  }
});
