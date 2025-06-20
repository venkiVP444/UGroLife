const API_BASE_URL = 'https://localhost:7086'; // Ensure this matches your backend API URL

// Function to show the message modal
function showMessageModal(title, message, isSuccess = false) {
    $('#modalTitle').text(title);
    $('#modalMessage').text(message);
    if (isSuccess) {
        $('#modalTitle').css('color', '#0cae00');
    } else {
        $('#modalTitle').css('color', '#dc3545');
    }
    $('#messageModal').addClass('active');
}

// Function to close the message modal
$('#closeModal').on('click', function () {
    $('#messageModal').removeClass('active');
});

// Function to redirect to login page
function redirectToLoginPage() {
    window.location.href = '/Home/Login'; // Adjust this path if your login page is different
}

$(document).ready(function () {
    const ordersList = $('#ordersList');

    // Function to fetch and display user orders
    async function fetchUserOrders() {
        const token = localStorage.getItem('jwtToken');

        if (!token) {
            showMessageModal('Authentication Required', 'Please log in to view your orders.', false);
            redirectToLoginPage();
            return;
        }

        ordersList.html('<p class="text-center text-gray-600 text-lg">Loading your orders...</p>');

        try {
            showLoading();
            const response = await fetch(`${API_BASE_URL}/api/Order/MyOrders`, {
                method: 'GET',
                headers: {
                    'Authorization': `Bearer ${token}`,
                    'Content-Type': 'application/json'
                }
            });

            if (response.status === 401) {
                hideLoading();
                showMessageModal('Unauthorized', 'Your session has expired or you are not authorized. Please log in again.', false);
                localStorage.removeItem('jwtToken');
                localStorage.removeItem('UserDetails');
                redirectToLoginPage();
                return;
            }

            if (!response.ok) {
                const errorData = await response.json();
                hideLoading();
                showMessageModal('Error Fetching Orders', errorData.message || 'Could not retrieve orders. Please try again later.', false);
                return;
            }

            const orders = await response.json();
            ordersList.empty(); // Clear previous content

            if (orders.length === 0) {
                hideLoading();
                ordersList.append(`
                                <div class="text-center bg-white p-8 rounded-lg shadow-md">
                                    <p class="text-xl text-gray-700 mb-4">You haven't placed any orders yet!</p>
                                    <a href="/Home/Index#microgreens" class="btn-primary">Start Shopping</a>
                                </div>
                            `);
                return;
            }

            orders.forEach(order => {
                const orderHtml = `
                                <div class="order-card border border-green-200 rounded-lg p-6 shadow-md bg-white text-gray-800">
                                    <div class="flex flex-col md:flex-row justify-between items-start md:items-center mb-4 border-b pb-4">
                                        <h4 class="text-2xl font-bold text-[#0cae00] mb-2 md:mb-0">Order #${order.orderId}</h4>
                                        <span class="text-xl font-semibold text-gray-900">Total: ₹${order.totalAmount.toFixed(2)}</span>
                                    </div>
                                    <div class="grid grid-cols-1 sm:grid-cols-2 gap-y-2 gap-x-6 mb-4 text-sm md:text-base">
                                        <p><strong>Date:</strong> ${new Date(order.orderDate).toLocaleDateString()} ${new Date(order.orderDate).toLocaleTimeString()}</p>
                                        <p><strong>Status:</strong> <span class="${order.status === 'Completed' ? 'text-green-600' : 'text-yellow-600'} font-semibold">${order.status}</span></p>
                                        <p><strong>Email:</strong> ${order.email}</p>
                                    </div>
                                    <h5 class="text-lg font-semibold mb-3 text-[#0cae00] border-t pt-4 mt-4">Items:</h5>
                                    <ul class="space-y-2">
                                        ${order.items.map(item => `
                                            <li class="flex justify-between items-center bg-gray-50 p-3 rounded-md border border-gray-100">
                                                <span class="text-gray-700">${item.productName} <span class="font-semibold">(x${item.quantity})</span></span>
                                                <span class="font-bold text-gray-900">₹${(item.quantity * item.unitPrice).toFixed(2)}</span>
                                            </li>
                                        `).join('')}
                                    </ul>
                                </div>
                            `;
                ordersList.append(orderHtml);
                hideLoading();
            });

        } catch (error) {
            console.error('Error fetching orders:', error);
            hideLoading();
            showMessageModal('Network Error', 'Could not connect to the server. Please check your internet connection and try again.', false);
        }
    }

    // Call fetchUserOrders when the page loads
    fetchUserOrders();
});