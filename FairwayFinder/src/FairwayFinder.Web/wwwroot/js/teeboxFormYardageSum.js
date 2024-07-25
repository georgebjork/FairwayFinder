
function yardageCalculator(htmxElementId) {
    function updateYardage() {

        const yardageIn = parseFloat($('#yardage-in').val()) || 0;
        const yardageOut = parseFloat($('#yardage-out').val()) || 0;

        const totalYardage = yardageIn + yardageOut;

        $('#yardage').val(totalYardage);
    }

// Event listeners for changes on YardageIn and YardageOut fields
    $('#yardage-in, #yardage-out').on('input', function () {
        updateYardage();
    });

// Run this after an htmx swap also
    document.addEventListener('htmx:afterSettle', function (event) {
        // Check if the swap happened on the element with the specific ID
        if (event.target.id === htmxElementId) {
            updateYardage();
        }

        // Event listeners for changes on YardageIn and YardageOut fields
        $('#yardage-in, #yardage-out').on('input', function () {
            updateYardage();
        });
    });

    // Initial calculation on page load
    updateYardage();
    
}


$(document).ready(function () {
    // Replace 'your-element-id' with the actual ID of the element where the HTMX swap occurs
    yardageCalculator('teebox-form');
});
