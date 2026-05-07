window.audioPlayer = {
    _objectUrl: null,

    // InputFile の id から blob URL を作成して audio 要素にセット
    setSourceFromInput: function (inputElementId, audioElementId) {
        const input = document.getElementById(inputElementId);
        const audio = document.getElementById(audioElementId);
        if (!input || !input.files || input.files.length === 0 || !audio) return false;

        if (this._objectUrl) {
            URL.revokeObjectURL(this._objectUrl);
        }
        this._objectUrl = URL.createObjectURL(input.files[0]);
        audio.src = this._objectUrl;
        return true;
    },

    // 保存済み URL を別の audio 要素にセット（Result ページ用）
    applySavedUrl: function (audioElementId) {
        if (!this._objectUrl) return false;
        const audio = document.getElementById(audioElementId);
        if (!audio) return false;
        audio.src = this._objectUrl;
        return true;
    },

    cleanup: function () {
        if (this._objectUrl) {
            URL.revokeObjectURL(this._objectUrl);
            this._objectUrl = null;
        }
    }
};
