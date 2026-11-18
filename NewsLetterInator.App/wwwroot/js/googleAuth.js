const AUTH_STORAGE_KEY = "gmailinator.google.auth";

let currentUser = null;
let lastAccessToken = null;

function ensureGoogleLoaded() {
    if (!window.google || !window.google.accounts || !window.google.accounts.oauth2) {
        throw new Error("Google Identity Services failed to load.");
    }
}

function safeReadStorage() {
    try {
        const raw = window.localStorage.getItem(AUTH_STORAGE_KEY);
        return raw ? JSON.parse(raw) : null;
    } catch {
        return null;
    }
}

function safeWriteStorage(payload) {
    try {
        window.localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(payload));
    } catch {
        // Ignore browser storage failures and continue with in-memory state.
    }
}

function clearStoredAuth() {
    currentUser = null;
    lastAccessToken = null;

    try {
        window.localStorage.removeItem(AUTH_STORAGE_KEY);
    } catch {
        // Ignore browser storage failures.
    }
}

async function fetchUser(accessToken) {
    const response = await fetch("https://www.googleapis.com/oauth2/v3/userinfo", {
        headers: {
            Authorization: `Bearer ${accessToken}`
        }
    });

    if (!response.ok) {
        throw new Error(`Failed to load user profile (${response.status}).`);
    }

    const payload = await response.json();
    return {
        name: payload.name ?? "",
        email: payload.email ?? "",
        picture: payload.picture ?? ""
    };
}

export async function getCurrentUser() {
    const cached = safeReadStorage();

    if (!cached || !cached.accessToken || !cached.expiresAtUtc || !cached.user) {
        clearStoredAuth();
        return null;
    }

    const expiresAt = new Date(cached.expiresAtUtc).getTime();
    if (!Number.isFinite(expiresAt) || expiresAt <= Date.now() + 60000) {
        clearStoredAuth();
        return null;
    }

    currentUser = cached.user;
    lastAccessToken = cached.accessToken;
    return currentUser;
}

function scopeIsSatisfied(cachedScope, requestedScope) {
    if (!cachedScope) {
        return false;
    }

    const granted = String(cachedScope).split(/\s+/).filter(Boolean);
    const requested = String(requestedScope).split(/\s+/).filter(Boolean);
    return requested.every(scope => granted.includes(scope));
}

export async function requestAccessToken(clientId, scope) {
    ensureGoogleLoaded();

    const cached = safeReadStorage();
    if (cached && cached.accessToken && cached.expiresAtUtc && scopeIsSatisfied(cached.scope, scope)) {
        const expiresAt = new Date(cached.expiresAtUtc).getTime();
        if (Number.isFinite(expiresAt) && expiresAt > Date.now() + 60000) {
            currentUser = cached.user ?? currentUser;
            lastAccessToken = cached.accessToken;
            return {
                accessToken: cached.accessToken,
                expiresAtUtc: cached.expiresAtUtc,
                scope: cached.scope ?? scope
            };
        }
    } else if (cached) {
        clearStoredAuth();
    }

    return new Promise((resolve, reject) => {
        let settled = false;
        let callbackTimeout = window.setTimeout(() => {
            if (!settled) {
                settled = true;
                reject(new Error("Google login timed out before a token was returned."));
            }
        }, 60000);

        const tokenClient = google.accounts.oauth2.initTokenClient({
            client_id: clientId,
            scope,
            callback: async (response) => {
                if (settled) {
                    return;
                }

                if (response.error) {
                    window.clearTimeout(callbackTimeout);
                    settled = true;
                    reject(new Error(response.error));
                    return;
                }

                try {
                    const accessToken = response.access_token;
                    const expiresAtUtc = new Date(Date.now() + ((response.expires_in ?? 3600) * 1000)).toISOString();
                    let user = cached?.user ?? {
                        name: "",
                        email: "",
                        picture: ""
                    };

                    lastAccessToken = accessToken;
                    currentUser = user;

                    const authPayload = {
                        accessToken,
                        expiresAtUtc,
                        scope: response.scope ?? scope,
                        user
                    };

                    safeWriteStorage(authPayload);
                    window.clearTimeout(callbackTimeout);
                    settled = true;

                    resolve({
                        accessToken,
                        expiresAtUtc,
                        scope: authPayload.scope
                    });

                    fetchUser(accessToken)
                        .then(profile => {
                            currentUser = profile;
                            safeWriteStorage({
                                ...authPayload,
                                user: profile
                            });
                        })
                        .catch(() => {
                            // The access token is usable even if the optional profile request fails.
                        });
                } catch (error) {
                    window.clearTimeout(callbackTimeout);
                    settled = true;
                    reject(error);
                }
            }
        });

        tokenClient.requestAccessToken({
            prompt: ""
        });
    });
}

export function signOut(clientId) {
    // Revoke any active token and clear the stored Google session.
    const tokenToRevoke = lastAccessToken || (safeReadStorage()?.accessToken ?? null);

    clearStoredAuth();

    if (window.google && window.google.accounts && window.google.accounts.oauth2 && tokenToRevoke) {
        google.accounts.oauth2.revoke(tokenToRevoke, () => {
            lastAccessToken = null;
        });
    }
}
