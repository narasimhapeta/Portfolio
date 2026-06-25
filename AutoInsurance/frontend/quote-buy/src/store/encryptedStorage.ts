import CryptoJS from 'crypto-js';

const STORAGE_KEY = 'qb_state';

// Key is derived from quoteNumber+zipCode — same as server SHA256 sessionToken input.
// When not available yet (step 1), fall back to a browser-session ephemeral key.
let _encryptionKey = sessionStorage.getItem('qb_ek') ?? '';

export function setEncryptionKey(quoteNumber: string, zipCode: string) {
  _encryptionKey = CryptoJS.SHA256(quoteNumber + zipCode).toString();
  sessionStorage.setItem('qb_ek', _encryptionKey);
}

export function clearEncryptionKey() {
  _encryptionKey = '';
  sessionStorage.removeItem('qb_ek');
}

export const encryptedStorage = {
  getItem(key: string): string | null {
    try {
      const raw = localStorage.getItem(key);
      if (!raw || !_encryptionKey) return null;
      const bytes = CryptoJS.AES.decrypt(raw, _encryptionKey);
      return bytes.toString(CryptoJS.enc.Utf8) || null;
    } catch {
      return null;
    }
  },
  setItem(key: string, value: string): void {
    if (!_encryptionKey) { localStorage.setItem(key, value); return; }
    const encrypted = CryptoJS.AES.encrypt(value, _encryptionKey).toString();
    localStorage.setItem(key, encrypted);
  },
  removeItem(key: string): void {
    localStorage.removeItem(key);
  },
};

export { STORAGE_KEY };
