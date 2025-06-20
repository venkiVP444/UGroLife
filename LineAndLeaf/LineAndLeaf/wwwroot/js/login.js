const API_BASE_URL = 'https://localhost:7086'
$(document).ready(function () {
    hideLoading();
    localStorage.clear();
    function showMessageModal(title, message) {
        $('#modalTitle').text(title);
        $('#modalMessage').text(message);
        $('#messageModal').addClass('active');
    }

    $('#closeModal').on('click', function () {
        $('#messageModal').removeClass('active');
    });

    $('#login-form').on('submit', function (e) {
        e.preventDefault();

        const emailOrUsername = $('#username').val();
        const password = $('#password').val();

        const loginData = {
            emailOrUsername: emailOrUsername,
            password: password
        };
        showLoading();
        fetch(`${API_BASE_URL}/api/Auth/login`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(loginData)
        })
            .then(response => {
                if (!response.ok) {
                    hideLoading();
                    return response.json().then(errorData => {
                        throw new Error(errorData.message || 'An unknown error occurred.');
                    });
                }
                return response.json();
            })
            .then(data => {
                if (data.success) {
                    localStorage.setItem('jwtToken', data.token);
                    hideLoading();
                    showMessageModal('Login Successful!', data.message + ' Welcome back to UGroLife!');
                    setTimeout(function () {
                        localStorage.setItem('UserDetails', JSON.stringify(data));
                        window.location.href = '/Home/Index';
                    }, 1500);
                } else {
                    hideLoading();
                    showMessageModal('Login Failed', data.message || 'Invalid credentials. Please try again.');
                }
            })
            .catch(error => {
                hideLoading();
                console.error('Error during login:', error);
                showMessageModal('Error', error.message || 'An unexpected error occurred during login. Please try again later.');
            });
    });
});