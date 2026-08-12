window.googleAuthentication = {
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
            }
        }, 100);
    }
};