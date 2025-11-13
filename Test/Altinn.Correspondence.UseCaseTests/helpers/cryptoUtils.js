import encoding from 'k6/encoding';

export function pemToBinary(pem) {
    const base64 = (pem || '')
        .replace(/-----BEGIN[\s\S]*?-----/g, '')
        .replace(/-----END[\s\S]*?-----/g, '')
        .replace(/\s+/g, '');
    return encoding.b64decode(base64, 'std');
}

export function utf8Encode(str) {
    const out = [];
    for (let i = 0; i < str.length; i++) {
        let code = str.charCodeAt(i);
        if (code < 0x80) {
            out.push(code);
        } else if (code < 0x800) {
            out.push(0xc0 | (code >> 6), 0x80 | (code & 0x3f));
        } else if (code < 0xd800 || code >= 0xe000) {
            out.push(0xe0 | (code >> 12), 0x80 | ((code >> 6) & 0x3f), 0x80 | (code & 0x3f));
        } else {
            i++;
            const code2 = str.charCodeAt(i);
            const u = 0x10000 + (((code & 0x3ff) << 10) | (code2 & 0x3ff));
            out.push(0xf0 | (u >> 18), 0x80 | ((u >> 12) & 0x3f), 0x80 | ((u >> 6) & 0x3f), 0x80 | (u & 0x3f));
        }
    }
    return new Uint8Array(out);
}