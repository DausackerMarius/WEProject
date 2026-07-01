// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.addEventListener('DOMContentLoaded', function () {
    var deleteModal = document.getElementById('deleteConfirmationModal');
    if (deleteModal) {
        deleteModal.addEventListener('show.bs.modal', function (event) {
            // Button that triggered the modal
            var button = event.relatedTarget;

            // Extract info from data-* attributes
            var deleteUrl = button.getAttribute('data-delete-url');
            var itemName = button.getAttribute('data-delete-item-name');
            var mainText = button.getAttribute('data-delete-main-text');
            var line1 = button.getAttribute('data-delete-item-line1');
            var line2 = button.getAttribute('data-delete-item-line2');
            var warningText = button.getAttribute('data-delete-warning-text');

            // Update the modal's content.
            var modalForm = deleteModal.querySelector('#delete-confirmation-form');
            var modalMainText = deleteModal.querySelector('#delete-modal-main-text');
            var modalItemName = deleteModal.querySelector('#delete-modal-item-name');
            var modalLine1 = deleteModal.querySelector('#delete-modal-item-line1');
            var modalLine2 = deleteModal.querySelector('#delete-modal-item-line2');
            var modalWarningText = deleteModal.querySelector('#delete-modal-warning-text');

            modalForm.action = deleteUrl;
            
            if(mainText) modalMainText.textContent = mainText;
            if(itemName) modalItemName.textContent = itemName;
            
            if(line1) {
                modalLine1.textContent = line1;
                modalLine1.style.display = 'block';
            } else {
                modalLine1.style.display = 'none';
            }

            if(line2) {
                modalLine2.textContent = line2;
                modalLine2.style.display = 'block';
            } else {
                modalLine2.style.display = 'none';
            }

            if(warningText) modalWarningText.textContent = warningText;
        });
    }
});
