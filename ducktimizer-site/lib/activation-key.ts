const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';

export function createActivationKey() {
  const bytes = crypto.getRandomValues(new Uint8Array(20));
  const chars = Array.from(bytes, byte => alphabet[byte % alphabet.length]).join('');
  return `DUCK-${chars.match(/.{4}/g)!.join('-')}`;
}

export function normalizeActivationKey(value: string) {
  return value.trim().toUpperCase();
}

export async function hashActivationKey(value: string) {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(normalizeActivationKey(value)));
  return Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, '0')).join('');
}

export const activationKeyPattern = /^DUCK(?:-[A-HJ-NP-Z2-9]{4}){5}$/;
