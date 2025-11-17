import encoding from 'k6/encoding';

export function pemToBinary(pem) {
    const base64 = (pem || '')
        .replace(/-----BEGIN[\s\S]*?-----/g, '')
        .replace(/-----END[\s\S]*?-----/g, '')
        .replace(/\s+/g, '');
    return encoding.b64decode(base64, 'std');
}

// Simple ASCII-only string to bytes conversion
// Works for base64url strings (JWT signing input) which are guaranteed ASCII
export function utf8Encode(str) {
    const arr = new Uint8Array(str.length);
    for (let i = 0; i < str.length; i++) {
        arr[i] = str.charCodeAt(i);
    }
    return arr;
}