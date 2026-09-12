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

function hydrateCachedSession() {
    const cached = safeReadStorage();

    if (!cached || !cached.accessToken || !cached.expiresAtUtc || !cached.user) {
        clearStoredAuth();
        return false;
    }

    const expiresAt = new Date(cached.expiresAtUtc).getTime();
    const isStillValid = expiresAt > Date.now() + 60000;

    if (!isStillValid) {
        clearStoredAuth();
        return false;
    }

    currentUser = cached.user;
    lastAccessToken = cached.accessToken;
    return true;
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

export function getCurrentUser() {
    if (!currentUser) {
        hydrateCachedSession();
    }

    return currentUser;
}

export function requestAccessToken(clientId, scope) {
    ensureGoogleLoaded();

    const cached = safeReadStorage();
    if (cached && cached.accessToken && cached.expiresAtUtc) {
        const expiresAt = new Date(cached.expiresAtUtc).getTime();
        if (expiresAt > Date.now() + 60000) {
            currentUser = cached.user ?? currentUser;
            lastAccessToken = cached.accessToken;
            return Promise.resolve({
                accessToken: cached.accessToken,
                expiresAtUtc: cached.expiresAtUtc,
                scope: cached.scope ?? scope
            });
        }
    }

    return new Promise((resolve, reject) => {
        const tokenClient = google.accounts.oauth2.initTokenClient({
            client_id: clientId,
            scope,
            callback: async (response) => {
                if (response.error) {
                    const shouldRetryWithConsent = Boolean(cached && cached.accessToken) && response.error !== "user_cancelled";

                    if (shouldRetryWithConsent) {
                        tokenClient.requestAccessToken({ prompt: "consent" });
                        return;
                    }

                    reject(new Error(response.error));
                    return;
                }

                try {
                    const accessToken = response.access_token;
                    const expiresAtUtc = new Date(Date.now() + ((response.expires_in ?? 3600) * 1000)).toISOString();
                    const user = await fetchUser(accessToken);

                    lastAccessToken = accessToken;
                    currentUser = user;

                    const authPayload = {
                        accessToken,
                        expiresAtUtc,
                        scope: response.scope ?? scope,
                        user
                    };

                    safeWriteStorage(authPayload);

                    resolve({
                        accessToken,
                        expiresAtUtc,
                        scope: authPayload.scope
                    });
                } catch (error) {
                    reject(error);
                }
            }
        });

        tokenClient.requestAccessToken({ prompt: cached && cached.accessToken ? "none" : "consent" });
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
