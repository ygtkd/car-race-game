mergeInto(LibraryManager.library, {
 RacerEmit: function(ptr) {
  if (window.Racer && window.Racer.receive) window.Racer.receive(JSON.parse(UTF8ToString(ptr)));
 }
});