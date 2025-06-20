const API_BASE_URL = 'https://localhost:7086'
$(document).ready(function () {
    function showMessageModal(title, message) {
        $('#modalTitle').text(title);
        $('#modalMessage').text(message);
        $('#messageModal').addClass('active');
    }

    $('#closeModal').on('click', function () {
        $('#messageModal').removeClass('active');
    });

    $('#register-form').on('submit', function (e) {
        e.preventDefault();
        const fullName = $('#fullName').val();
        const email = $('#email').val();
        const password = $('#password').val();
        const confirmPassword = $('#confirmPassword').val();
        const mobile = $('#phone').val();
        if (password !== confirmPassword) {
            showMessageModal('Registration Failed', 'Passwords do not match. Please try again.');
            return;
        }

        const registrationData = {
            fullName: fullName,
            email: email,
            mobile: mobile,
            password: password
        };
        showLoading();
        fetch(`${API_BASE_URL}/api/Auth/register`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(registrationData)
        })
            .then(response => {
                // Check if response is OK (200-299 status codes)
                if (!response.ok) {
                    // If not OK, parse the error response and throw it
                    return response.json().then(errorData => {
                        throw new Error(errorData.message || 'An unknown error occurred.');
                    });
                }
                return response.json(); // Parse the JSON success response
            }) // <-- Check if this ')' is missing
            .then(data => {
                // Handle successful registration
                if (data.success) {
                    hideLoading();
                    showMessageModal('Registration Successful!', data.message + ' Redirecting to login...');
                    setTimeout(function () {
                        window.location.href = '/Home/Login'; // Redirect to your login page
                    }, 2000);
                } else {
                    // This else block might be redundant if !response.ok handles most errors,
                    // but keeps it in case the API returns {success: false} with a 200 status.
                    hideLoading();
                    showMessageModal('Registration Failed', data.message || 'An error occurred during registration. Please try again.');
                }
            }) // <-- Check if this ')' is missing
            .catch(error => {
                // Handle any network errors or errors thrown from the .then(response => ...) block
                console.error('Error during registration:', error);
                hideLoading();
                showMessageModal('Error', error.message || 'An unexpected error occurred during registration. Please try again later.');
            })

    });
});