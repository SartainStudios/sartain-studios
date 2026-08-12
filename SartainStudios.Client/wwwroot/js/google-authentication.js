(function () {
    const pendingKey = 'sartainstudios.google.pending';
    const redirectPath = '/sign-in';

    let capturedResult = null;

    function captureFromHash() {
        try {
            const hash = window.location.hash;
            if (!hash || hash.length < 2) {
                return;
            }
            const parameters = new URLSearchParams(hash.substring(1));
            const idToken = parameters.get('id_token');
            const error = parameters.get('error');
            if (!idToken && !error) {
                return;
            }
            capturedResult = {
                credential: idToken || '',
                state: parameters.get('state') || '',
                error: error || ''
            };
            window.history.replaceState(null, '', window.location.pathname + window.location.search);
        } catch (e) {
            capturedResult = null;
        }
    }

    function randomToken() {
        const bytes = new Uint8Array(16);
        window.crypto.getRandomValues(bytes);
        return Array.from(bytes).map(function (b) {
            return b.toString(16).padStart(2, '0');
        }).join('');
    }

    function isStandalone() {
        try {
            return window.navigator.standalone === true ||
                (!!window.matchMedia && (
                    window.matchMedia('(display-mode: standalone)').matches ||
                    window.matchMedia('(display-mode: fullscreen)').matches ||
                    window.matchMedia('(display-mode: minimal-ui)').matches));
        } catch (e) {
            return false;
        }
    }

    function readPending() {
        try {
            return JSON.parse(window.localStorage.getItem(pendingKey) || 'null');
        } catch (e) {
            return null;
        }
    }

    captureFromHash();

    window.googleAuthentication = {
        shouldUseRedirect: function () {
            return isStandalone();
        },

        isStandalone: isStandalone,

        hasRedirectResult: function () {
            return capturedResult !== null;
        },

        consumeRedirectResult: function () {
            const result = capturedResult;
            capturedResult = null;
            if (!result) {
                return null;
            }
            const pending = readPending();
            try {
                window.localStorage.removeItem(pendingKey);
            } catch (e) {
                // ignore
            }
            if (result.error) {
                return {
                    credential: '',
                    returnUrl: (pending && pending.returnUrl) || '',
                    error: result.error === 'access_denied'
                        ? 'Google sign-in was cancelled.'
                        : 'Google sign-in failed. Please try again.'
                };
            }
            if (!pending || !pending.state || pending.state !== result.state) {
                return {
                    credential: '',
                    returnUrl: '',
                    error: 'Google sign-in could not be verified. Please try again.'
                };
            }
            return {
                credential: result.credential,
                returnUrl: pending.returnUrl || '',
                error: ''
            };
        },

        signInWithRedirect: function (clientId, returnUrl) {
            const nonce = randomToken();
            const state = randomToken();
            try {
                window.localStorage.setItem(pendingKey, JSON.stringify({
                    nonce: nonce,
                    state: state,
                    returnUrl: returnUrl || '/'
                }));
            } catch (e) {
                // ignore
            }
            const parameters = new URLSearchParams({
                client_id: clientId,
                response_type: 'id_token',
                scope: 'openid email profile',
                redirect_uri: window.location.origin + redirectPath,
                nonce: nonce,
                state: state,
                prompt: 'select_account'
            });
            window.location.assign('https://accounts.google.com/o/oauth2/v2/auth?' + parameters.toString());
        },

        render: function (elementId, clientId, dotNetRef) {
            const doInit = function () {
                const element = document.getElementById(elementId);
                if (!element) {
                    return;
                }
                google.accounts.id.initialize({
                    client_id: clientId,
                    callback: function (response) {
                        dotNetRef.invokeMethodAsync('OnGoogleCredential', response.credential);
                    },
                    use_fedcm_for_button: true,
                    use_fedcm_for_prompt: true
                });
                google.accounts.id.renderButton(element, {
                    theme: 'outline',
                    size: 'large',
                    type: 'standard',
                    text: 'continue_with',
                    shape: 'rectangular',
                    logo_alignment: 'left',
                    width: element.offsetWidth || 320
                });
            };
            const isReady = function () {
                return window.google && window.google.accounts && window.google.accounts.id;
            };
            if (isReady()) {
                doInit();
                return;
            }
            let attempts = 0;
            const timer = setInterval(function () {
                attempts += 1;
                if (isReady()) {
                    clearInterval(timer);
                    doInit();
                } else if (attempts > 100) {
                    clearInterval(timer);
                    dotNetRef.invokeMethodAsync('OnGoogleScriptUnavailable');
                }
            }, 100);
        }
    };
}());