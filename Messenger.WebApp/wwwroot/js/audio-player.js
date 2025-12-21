// ======================================================================
//       AUDIO PLAYER (FINAL VERSION WITH SINGLE BUTTON + INDEXEDDB)
// ======================================================================

$(document).ready(function () {

    // ================================================================
    //                        SVG ICONS
    // ================================================================

    const SVG_PLAY = `
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" fill="#000" viewBox="0 0 24 24">
          <path d="M4 12V8.44C4 4.02 7.13 2.21 10.96 4.42L17.14 7.98C20.97 10.19 20.97 13.81 17.14 16.02L10.96 19.58C7.13 21.79 4 19.98 4 15.56V12Z"
           stroke="#fff" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>`;

    const SVG_PAUSE = `
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" fill="#000" viewBox="0 0 24 24">
          <path d="M10 8H8v8h2V8zm6 0h-2v8h2V8z"/>
        </svg>`;

    const SVG_SPINNER = `
        <img src="/chatzy/assets/iconsax/spinner.svg" style="width:20px;height:20px;" />
    `;

    // ================================================================
    //         SINGLE-PLAY STATE (only one audio at a time)
    // ================================================================
    // Ensure only one audio element plays at once. When a new audio starts,
    // the previously playing audio will be paused and its UI updated.
    let currentlyPlayingAudio = null;           // HTMLAudioElement currently playing
    let currentlyPlayingContainer = null;       // jQuery container for currently playing audio

    // ================================================================
    //                  AUDIO EVENT BINDING (PLAY/PAUSE)
    // ================================================================

    function setupAudioEvents(audio, $container) {
        if ($(audio).data('events-attached')) return;

        const $btn = $container.find('.voice-playback-btn');
        const $progress = $container.find('.voice-timeline-progress');
        const $handle = $container.find('.voice-timeline-handle');
        const $durationDisplay = $container.find('.voice-duration-display');

        // When this audio starts to play, pause any other playing audio and update UI.
        $(audio).on('play', function () {
            try {
                // If another audio is playing, pause it and update its button UI
                if (currentlyPlayingAudio && currentlyPlayingAudio !== this) {
                    try { currentlyPlayingAudio.pause(); } catch (err) { /* ignore */ }
                    if (currentlyPlayingContainer && currentlyPlayingContainer.length) {
                        currentlyPlayingContainer.find('.voice-playback-btn').html(SVG_PLAY);
                    }
                }
            } catch (err) {
                // defensive
            }

            // set this as currently playing
            currentlyPlayingAudio = this;
            currentlyPlayingContainer = $container;
            $btn.html(SVG_PAUSE);
        });

        $(audio).on('pause', function () {
            $btn.html(SVG_PLAY);
            // clear global pointer if this was the playing audio
            if (currentlyPlayingAudio === this) {
                currentlyPlayingAudio = null;
                currentlyPlayingContainer = null;
            }
        });

        $(audio).on('ended', function () {
            //audio.currentTime = 0;
            $btn.html(SVG_PLAY);
            if (currentlyPlayingAudio === this) {
                currentlyPlayingAudio = null;
                currentlyPlayingContainer = null;
            }
        });

        $(audio).on('timeupdate', function () {
            if (isFinite(this.duration)) {
                const p = (this.currentTime / this.duration) * 100;
                $progress.css('width', `${p}%`);
                $handle.css('left', `${p}%`);
                $durationDisplay.text(formatAudioTime(this.currentTime));
            }
        });

        $(audio).data('events-attached', true);
    }


    function formatAudioTime(t) {
        if (isNaN(t) || !isFinite(t)) return "0:00";
        const m = Math.floor(t / 60);
        const s = Math.floor(t % 60).toString().padStart(2, '0');
        return `${m}:${s}`;
    }


    // ================================================================
    //                   MAIN PLAY / DOWNLOAD HANDLER
    // ================================================================

    $(document).on('click', '.voice-playback-btn', async function (e) {
        e.stopPropagation();

        const $container = $(this).closest('.audio-player-container');
        const fileId = $container.data('file-id');
        const audio = $container.find('audio')[0];

        const $btn = $(this);
        const originalHTML = $btn.html();

        const $timeline = $container.find('.voice-timeline-container');
        const $duration = $container.find('.voice-duration-display');

        // ---------- 1) Check cached ----------
        let cached = await indexedAudioDB.getFile(fileId);
        if (cached?.blob) {

            if (!audio.src) {
                audio.src = createAndMarkObjectUrl(cached.blob);
            }

            setupAudioEvents(audio, $container);
            $timeline.removeClass('d-none');
            $duration.removeClass('d-none');

            // Before toggling play, ensure any other audio is paused
            if (currentlyPlayingAudio && currentlyPlayingAudio !== audio) {
                try { currentlyPlayingAudio.pause(); } catch (err) { /* ignore */ }
                if (currentlyPlayingContainer && currentlyPlayingContainer.length) {
                    currentlyPlayingContainer.find('.voice-playback-btn').html(SVG_PLAY);
                }
            }

            return audio.paused ? audio.play() : audio.pause();
        }

        // ---------- 2) Download ----------
        $btn.html(SVG_SPINNER);

        try {
            const file = await downloader.downloadFile(fileId);
            await indexedAudioDB.saveFile(fileId, file.blob, file.fileName);

            const blobUrl = createAndMarkObjectUrl(file.blob);
            audio.src = blobUrl;

            decodeDuration(file.blob).then(duration => {
                $duration.text(formatAudioTime(duration));
                file.duration = duration;
                indexedAudioDB.saveFile(fileId, file.blob, file.fileName);
            });

            setupAudioEvents(audio, $container);
            $timeline.removeClass('d-none');
            $duration.removeClass('d-none');

            // Pause currently playing audio (if different) before starting this one
            if (currentlyPlayingAudio && currentlyPlayingAudio !== audio) {
                try { currentlyPlayingAudio.pause(); } catch (err) { /* ignore */ }
                if (currentlyPlayingContainer && currentlyPlayingContainer.length) {
                    currentlyPlayingContainer.find('.voice-playback-btn').html(SVG_PLAY);
                }
            }

            audio.play();
        }
        catch {
            alert("خطا در دانلود فایل صوتی");
        }
        finally {
            if (audio.paused) $btn.html(originalHTML);
        }
    });


    // ================================================================
    //                    TIMELINE SEEKING
    // ================================================================

    $(document).on('click', '.voice-timeline-container', function (e) {
        const audio = $(this).closest('.audio-player-container').find('audio')[0];
        if (!audio || !isFinite(audio.duration)) return;

        const x = e.pageX - $(this).offset().left;
        audio.currentTime = (x / $(this).width()) * audio.duration;
    });


    // ================================================================
    //                      IndexedDB 
    // ================================================================

    class IndexedAudioDB {
        constructor(name = 'audioDB', store = 'files', opt = {}) {
            this.dbName = name;
            this.storeName = store;
            this.maxEntries = opt.maxEntries || 200;
            this.maxTotalBytes = opt.maxTotalBytes || 200 * 1024 * 1024;
            this._dbp = null;
        }

        openDB() {
            if (this._dbp) return this._dbp;

            this._dbp = new Promise((resolve, reject) => {
                const req = indexedDB.open(this.dbName, 1);

                req.onupgradeneeded = e => {
                    const db = e.target.result;
                    const store = db.createObjectStore(this.storeName, { keyPath: 'id' });
                    store.createIndex('ts', 'timestamp');
                };

                req.onsuccess = e => resolve(e.target.result);
                req.onerror = e => reject(e.target.error);
            });

            return this._dbp;
        }

        async listAll() {
            const db = await this.openDB();
            return new Promise(resolve => {
                const tx = db.transaction(this.storeName, 'readonly');
                const store = tx.objectStore(this.storeName);
                const list = [];

                store.openCursor().onsuccess = e => {
                    const cursor = e.target.result;
                    if (cursor) {
                        list.push(cursor.value);
                        cursor.continue();
                    } else {
                        resolve(list);
                    }
                };
            });
        }

        async getFile(id) {
            const db = await this.openDB();
            return new Promise(resolve => {
                const tx = db.transaction(this.storeName, 'readwrite');
                const store = tx.objectStore(this.storeName);
                const req = store.get(id);

                req.onsuccess = e => {
                    const val = e.target.result;
                    if (val) {
                        val.timestamp = Date.now();
                        store.put(val);
                    }
                    resolve(val);
                };
                req.onerror = () => resolve(null);
            });
        }

        async saveFile(id, blob, fileName) {
            const db = await this.openDB();
            return new Promise(resolve => {
                const tx = db.transaction(this.storeName, 'readwrite');
                tx.objectStore(this.storeName).put({
                    id,
                    blob,
                    fileName,
                    timestamp: Date.now(),
                    size: blob.size
                });
                tx.oncomplete = () => this.enforceLimits().then(() => resolve());
            });
        }

        async enforceLimits() {
            const db = await this.openDB();
            return new Promise(resolve => {
                const tx = db.transaction(this.storeName, 'readwrite');
                const store = tx.objectStore(this.storeName);
                const items = [];

                store.openCursor().onsuccess = e => {
                    const c = e.target.result;
                    if (c) {
                        items.push(c.value);
                        c.continue();
                    } else {
                        items.sort((a, b) => a.timestamp - b.timestamp);

                        let total = items.reduce((s, x) => s + x.size, 0);

                        while (total > this.maxTotalBytes && items.length) {
                            const rm = items.shift();
                            total -= rm.size;
                            store.delete(rm.id);
                        }

                        while (items.length > this.maxEntries) {
                            const rm = items.shift();
                            store.delete(rm.id);
                        }

                        resolve();
                    }
                };
            });
        }
    }

    window.indexedAudioDB = new IndexedAudioDB();


    // ================================================================
    //                OBJECT URL MANAGEMENT
    // ================================================================

    const createdURLs = new Set();

    function createAndMarkObjectUrl(blob) {
        const url = URL.createObjectURL(blob);
        createdURLs.add(url);
        return url;
    }

    window.addEventListener("beforeunload", () => {
        for (const u of createdURLs) {
            try { URL.revokeObjectURL(u); } catch { }
        }
    });


    // ================================================================
    //                        FILE DOWNLOADER
    // ================================================================

    class FileDownloader {
        constructor(endpoint) {
            this.endpoint = endpoint;
        }

        async downloadFile(fileId) {
            const cached = await indexedAudioDB.getFile(fileId);
            if (cached?.blob) return { blob: cached.blob, fileName: cached.fileName };

            const res = await fetch(this.endpoint, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ FileId: fileId })
            });

            if (!res.ok) throw new Error("Download failed");

            const blob = await res.blob();
            return { blob, fileName: "audio" };
        }
    }

    window.downloader = new FileDownloader("/api/chat/downloadBlobFileById");


    // ================================================================
    //       DYNAMIC PRELOAD FOR OLD/NEW MESSAGES (MAIN OPTIMIZED)
    // ================================================================

    async function preloadCachedFor(element) {
        if (!element) return;

        const containers = element.querySelectorAll('.audio-player-container[data-file-id]');
        if (containers.length === 0) return;

        const cachedFiles = await indexedAudioDB.listAll();
        if (!cachedFiles?.length) return;

        const map = new Map();
        for (const c of cachedFiles) map.set(c.id, c);

        containers.forEach(async container => {
            const fileId = container.dataset.fileId;
            if (!map.has(fileId)) return;

            const cached = map.get(fileId);
            const audio = container.querySelector('audio');
            const durationEl = container.querySelector('.voice-duration-display');

            if (!audio.src) {
                audio.src = createAndMarkObjectUrl(cached.blob);
            }

            if (cached.duration) {
                durationEl.textContent = formatAudioTime(cached.duration);
                return;
            }

            decodeDuration(cached.blob).then(duration => {
                durationEl.textContent = formatAudioTime(duration);
                cached.duration = duration;
                indexedAudioDB.saveFile(fileId, cached.blob, cached.fileName);
            });
        });
    }

    // expose globally
    window.preloadCachedFor = preloadCachedFor;


    // ================================================================
    //                  DECODE DURATION USING AudioContext
    // ================================================================

    async function decodeDuration(blob) {
        const ctx = new (window.AudioContext || window.webkitAudioContext)();
        const buf = await blob.arrayBuffer();
        const decoded = await ctx.decodeAudioData(buf);
        return decoded.duration;
    }


    // ================================================================
    //                 INITIAL LOAD (First messages only)
    // ================================================================

    preloadCachedFor(document);

});