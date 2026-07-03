// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.addEventListener('DOMContentLoaded', function () {
    var deleteModal = document.getElementById('deleteConfirmationModal');

    if (!deleteModal) {
        return;
    }

    deleteModal.addEventListener('show.bs.modal', function (event) {
        var button = event.relatedTarget;

        if (!button) {
            return;
        }

        var deleteUrl = button.getAttribute('data-delete-url');
        var itemName = button.getAttribute('data-delete-item-name');
        var mainText = button.getAttribute('data-delete-main-text');
        var line1 = button.getAttribute('data-delete-item-line1');
        var line2 = button.getAttribute('data-delete-item-line2');
        var warningText = button.getAttribute('data-delete-warning-text');

        var modalForm = deleteModal.querySelector('#delete-confirmation-form');
        var modalMainText = deleteModal.querySelector('#delete-modal-main-text');
        var modalItemName = deleteModal.querySelector('#delete-modal-item-name');
        var modalLine1 = deleteModal.querySelector('#delete-modal-item-line1');
        var modalLine2 = deleteModal.querySelector('#delete-modal-item-line2');
        var modalWarningText = deleteModal.querySelector('#delete-modal-warning-text');

        if (modalForm && deleteUrl) {
            modalForm.setAttribute('action', deleteUrl);
        }

        if (modalMainText && mainText) {
            modalMainText.textContent = mainText;
        }

        if (modalItemName && itemName) {
            modalItemName.textContent = itemName;
        }

        if (modalLine1) {
            if (line1) {
                modalLine1.textContent = line1;
                modalLine1.style.display = 'block';
            } else {
                modalLine1.textContent = '';
                modalLine1.style.display = 'none';
            }
        }

        if (modalLine2) {
            if (line2) {
                modalLine2.textContent = line2;
                modalLine2.style.display = 'block';
            } else {
                modalLine2.textContent = '';
                modalLine2.style.display = 'none';
            }
        }

        if (modalWarningText && warningText) {
            modalWarningText.textContent = warningText;
        }
    });
});
