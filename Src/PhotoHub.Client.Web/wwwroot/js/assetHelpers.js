function downloadAsset(url, filename) {
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.style.display = 'none';
    document.body.appendChild(a);
    a.click();
    setTimeout(() => document.body.removeChild(a), 100);
}

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
