window.phoneInput = {
    restrict: function (dotNetRef, elementId) {
        const container = document.getElementById(elementId);
        if (!container) return;
        const input = container.querySelector('input');
        if (!input || input.dataset.phoneRestricted) return;
        input.dataset.phoneRestricted = 'true';

        input.addEventListener('keydown', function (e) {
            // Allow: backspace, delete, tab, escape, enter, arrows
            if ([8, 9, 27, 13, 46].indexOf(e.keyCode) !== -1 ||
                (e.keyCode >= 35 && e.keyCode <= 40) ||
                // Allow Ctrl+A/C/V/X
                ((e.keyCode === 65 || e.keyCode === 67 || e.keyCode === 86 || e.keyCode === 88) && (e.ctrlKey || e.metaKey))) {
                return;
            }
            // Block if not a number
            if ((e.shiftKey || (e.keyCode < 48 || e.keyCode > 57)) && (e.keyCode < 96 || e.keyCode > 105)) {
                e.preventDefault();
            }
        });

        // Also handle paste - strip non-digits
        input.addEventListener('paste', function (e) {
            e.preventDefault();
            var text = (e.clipboardData || window.clipboardData).getData('text');
            var digits = text.replace(/\D/g, '').substring(0, 10);
            document.execCommand('insertText', false, digits);
        });
    }
};
