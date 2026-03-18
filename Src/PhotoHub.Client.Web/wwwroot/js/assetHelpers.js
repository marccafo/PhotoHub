function downloadFileFromBytes(fileName, contentType, bytes) {
    const blob = new Blob([new Uint8Array(bytes)], { type: contentType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.style.display = 'none';
    document.body.appendChild(a);
    a.click();
    setTimeout(() => {
        URL.revokeObjectURL(url);
        document.body.removeChild(a);
    }, 100);
}

function downloadAsset(url, filename) {
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.style.display = 'none';
    document.body.appendChild(a);
    a.click();
    setTimeout(() => document.body.removeChild(a), 100);
}

window.assetTransition = {
    _rect: null,

    saveOrigin: function (element) {
        if (!element) return;
        var rect = element.getBoundingClientRect();
        this._rect = { top: rect.top, left: rect.left, right: rect.right, bottom: rect.bottom };
    },

    playEnterAnimation: function (element) {
        if (!element) return;
        var rect = this._rect;
        this._rect = null;

        var vw = window.innerWidth;
        var vh = window.innerHeight;

        if (rect && rect.width > 0 && rect.height > 0) {
            element.animate([
                {
                    clipPath: 'inset(' + rect.top + 'px ' + (vw - rect.right) + 'px ' + (vh - rect.bottom) + 'px ' + rect.left + 'px round 8px)',
                    opacity: '0.75'
                },
                {
                    clipPath: 'inset(0px 0px 0px 0px round 0px)',
                    opacity: '1'
                }
            ], { duration: 360, easing: 'cubic-bezier(0.4, 0, 0.2, 1)', fill: 'none' });
        } else {
            element.animate(
                [{ opacity: '0' }, { opacity: '1' }],
                { duration: 220, easing: 'ease-out', fill: 'none' }
            );
        }
    }
};

async function shareOrCopyUrl(url, title) {
    if (navigator.share) {
        try {
            await navigator.share({ url: url, title: title });
            return 'shared';
        } catch (e) {
            if (e.name === 'AbortError') return 'aborted';
        }
    }
    try {
        await navigator.clipboard.writeText(url);
        return 'copied';
    } catch {
        return 'error';
    }
}
