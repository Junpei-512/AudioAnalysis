(() => {
    const S = {
        audioCtx: null, analyser: null, source: null, animId: null,
        audioEl: null, sections: [], duration: 0, sampleRate: 44100,
        liveWaveCanvas: null, liveFftCanvas: null, liveHarmCanvas: null,
    };

    // ─── 公開 API ────────────────────────────────
    window.realtimeAnalyzer = {

        init(audioId, liveWaveId, liveFftId, liveHarmId, sectionsJson, duration) {
            const audio = document.getElementById(audioId);
            if (!audio) return;

            S.audioEl  = audio;
            S.sections = sectionsJson ? JSON.parse(sectionsJson) : [];
            S.duration  = duration || 0;

            S.liveWaveCanvas = document.getElementById(liveWaveId);
            S.liveFftCanvas  = document.getElementById(liveFftId);
            S.liveHarmCanvas = document.getElementById(liveHarmId);

            // blob URL 適用
            if (window.audioPlayer?._objectUrl)
                audio.src = window.audioPlayer._objectUrl;

            audio.addEventListener('play',  onPlay);
            audio.addEventListener('pause', stopLoop);
            audio.addEventListener('ended', stopLoop);

            // Canvas DPI 対応
            [S.liveWaveCanvas, S.liveFftCanvas, S.liveHarmCanvas].forEach(resizeCanvas);
        },

        dispose() {
            stopLoop();
            try { S.source?.disconnect(); } catch (_) {}
            S.audioCtx?.close();
            Object.assign(S, { audioCtx: null, analyser: null, source: null });
        },
    };

    // ─── Web Audio 初期化 ────────────────────────
    function onPlay() {
        if (!S.audioCtx) {
            S.audioCtx = new (window.AudioContext || window.webkitAudioContext)();
            S.sampleRate = S.audioCtx.sampleRate;

            S.analyser = S.audioCtx.createAnalyser();
            S.analyser.fftSize = 4096;          // 周波数分解能を上げる
            S.analyser.smoothingTimeConstant = 0.8;

            S.source = S.audioCtx.createMediaElementSource(S.audioEl);
            S.source.connect(S.analyser);
            S.analyser.connect(S.audioCtx.destination);
        }
        if (S.audioCtx.state === 'suspended') S.audioCtx.resume();
        startLoop();
    }

    // ─── アニメーションループ ─────────────────────
    function startLoop() {
        if (S.animId) return;

        const bufLen   = S.analyser.frequencyBinCount; // fftSize / 2 = 2048
        const freqData = new Uint8Array(bufLen);
        const timeData = new Uint8Array(bufLen);

        const wCtx  = S.liveWaveCanvas?.getContext('2d');
        const fCtx  = S.liveFftCanvas?.getContext('2d');
        const hCtx  = S.liveHarmCanvas?.getContext('2d');

        function frame() {
            S.animId = requestAnimationFrame(frame);

            S.analyser.getByteTimeDomainData(timeData);
            S.analyser.getByteFrequencyData(freqData);

            drawOscilloscope(wCtx, S.liveWaveCanvas, timeData, bufLen);
            drawSpectrum(fCtx, S.liveFftCanvas, freqData, bufLen);
            drawHarmonics(hCtx, S.liveHarmCanvas, freqData, bufLen, S.sampleRate);

            const audio = S.audioEl;
            if (audio?.duration > 0) {
                updatePlayhead(audio.currentTime, audio.duration);
                highlightSection(audio.currentTime);
            }
        }
        frame();
    }

    function stopLoop() {
        cancelAnimationFrame(S.animId);
        S.animId = null;
        [S.liveWaveCanvas, S.liveFftCanvas, S.liveHarmCanvas].forEach(clearCanvas);
    }

    // ─── 描画: オシロスコープ ─────────────────────
    function drawOscilloscope(ctx, canvas, data, bufLen) {
        if (!ctx || !canvas) return;
        const W = canvas.width, H = canvas.height;
        ctx.fillStyle = '#0f1117';
        ctx.fillRect(0, 0, W, H);

        ctx.strokeStyle = '#2d3050';
        ctx.lineWidth = 1;
        ctx.beginPath(); ctx.moveTo(0, H / 2); ctx.lineTo(W, H / 2); ctx.stroke();

        ctx.strokeStyle = '#4a9eff';
        ctx.lineWidth = 1.5;
        ctx.beginPath();
        const step = W / bufLen;
        for (let i = 0; i < bufLen; i++) {
            const y = ((data[i] / 128) - 1) * (H / 2) + H / 2;
            i === 0 ? ctx.moveTo(0, y) : ctx.lineTo(i * step, y);
        }
        ctx.stroke();
    }

    // ─── 描画: FFT スペクトル ─────────────────────
    function drawSpectrum(ctx, canvas, data, bufLen) {
        if (!ctx || !canvas) return;
        const W = canvas.width, H = canvas.height;
        ctx.fillStyle = '#0f1117';
        ctx.fillRect(0, 0, W, H);

        const barW = W / bufLen;
        for (let i = 0; i < bufLen; i++) {
            const h   = (data[i] / 255) * H;
            const hue = 220 + (i / bufLen) * 60;
            ctx.fillStyle = `hsl(${hue},70%,55%)`;
            ctx.fillRect(i * barW, H - h, Math.max(barW - 0.3, 0.3), h);
        }
    }

    // ─── 描画: リアルタイム倍音 (HPS) ─────────────
    function drawHarmonics(ctx, canvas, freqData, bufLen, sampleRate) {
        if (!ctx || !canvas) return;
        const W = canvas.width, H = canvas.height;
        ctx.fillStyle = '#0f1117';
        ctx.fillRect(0, 0, W, H);

        const freqRes = sampleRate / (bufLen * 2); // Hz/bin

        // HPS で基音ビンを推定
        const MAX_H  = 4;
        const minBin = Math.ceil(50 / freqRes);
        const maxBin = Math.min(Math.floor(2000 / freqRes), Math.floor(bufLen / MAX_H) - 1);

        let bestBin = minBin, bestVal = 0;
        for (let k = minBin; k <= maxBin; k++) {
            let prod = freqData[k] / 255;
            for (let r = 2; r <= MAX_H; r++) prod *= (freqData[k * r] ?? 0) / 255;
            if (prod > bestVal) { bestVal = prod; bestBin = k; }
        }

        const fundamental = bestBin * freqRes;

        // 倍音バーを描画（H1〜H8）
        const BARS      = 8;
        const barSlotH  = Math.floor((H - 22) / BARS); // 下部に基音周波数テキスト用スペース
        const fontSize  = Math.min(barSlotH - 4, 11);

        ctx.font        = `${fontSize}px monospace`;
        ctx.textBaseline = 'middle';

        for (let h = 1; h <= BARS; h++) {
            const bin  = Math.round(bestBin * h);
            const mag  = bin < bufLen ? freqData[bin] / 255 : 0;
            const barW = mag * W * 0.88;      // 88% をバー領域に
            const y    = (h - 1) * barSlotH;
            const cy   = y + barSlotH / 2;
            const hue  = 220 + h * 15;
            const freq = fundamental * h;

            // バー
            ctx.fillStyle = `hsl(${hue},70%,${40 + mag * 20}%)`;
            ctx.fillRect(0, y + 2, barW, barSlotH - 4);

            // ラベル (H1〜H8 と周波数)
            ctx.fillStyle = mag > 0.3 ? '#0f1117' : '#8888aa';
            ctx.fillText(`H${h}`, 4, cy);
            ctx.fillStyle = '#8888aa';
            ctx.fillText(`${freq < 1000 ? freq.toFixed(0) + 'Hz' : (freq / 1000).toFixed(2) + 'kHz'}`, W * 0.88 + 4, cy);
        }

        // 基音周波数
        ctx.fillStyle = '#4a9eff';
        ctx.font = `bold ${fontSize}px monospace`;
        ctx.fillText(`基音: ${fundamental.toFixed(1)} Hz`, 4, H - 8);
    }

    // ─── 再生ヘッド更新 ──────────────────────────
    function updatePlayhead(t, dur) {
        const line = document.getElementById('wavePlayhead');
        if (!line) return;
        const x = (t / dur) * 1000;
        line.setAttribute('x1', x.toFixed(1));
        line.setAttribute('x2', x.toFixed(1));
        line.setAttribute('opacity', '1');
    }

    // ─── 調性セクションハイライト ─────────────────
    function highlightSection(t) {
        document.querySelectorAll('.timeline-section').forEach((el, i) => {
            const sec = S.sections[i];
            if (!sec) return;
            el.classList.toggle('ts-active', t >= sec.startSec && t < sec.endSec);
        });
    }

    // ─── ユーティリティ ──────────────────────────
    function clearCanvas(canvas) {
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        ctx.fillStyle = '#0f1117';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
    }

    function resizeCanvas(canvas) {
        if (!canvas) return;
        const dpr  = window.devicePixelRatio || 1;
        const rect = canvas.getBoundingClientRect();
        if (rect.width === 0) return;
        canvas.width  = rect.width  * dpr;
        canvas.height = rect.height * dpr;
        canvas.getContext('2d').scale(dpr, dpr);
    }
})();
