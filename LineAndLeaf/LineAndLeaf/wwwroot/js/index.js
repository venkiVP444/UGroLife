const API_BASE_URL = 'https://localhost:7086'; // Example: 'https://your-live-api.com'

// Your global cart array, initialized from localStorage or as an empty array.
let cartItems = JSON.parse(localStorage.getItem('UGroLifeCart')) || [];

// Function to retrieve the JWT token from local storage.
function getAuthToken() {
    return localStorage.getItem('jwtToken');
}

// Function to display a modal message to the user.
// Requires a modal HTML structure with IDs: #messageModal, #modalTitle, #modalMessage, #closeModal.
function showMessageModal(title, message, isSuccess = false) {
    $('#modalTitle').text(title);
    $('#modalMessage').text(message);
    if (isSuccess) {
        $('#modalTitle').css('color', '#0cae00'); // Green for success
    } else {
        $('#modalTitle').css('color', '#dc3545'); // Red for error
    }
    $('#messageModal').addClass('active'); // Assumes a CSS class 'active' to show the modal
}

// Function to redirect the user to the login page.
function redirectToLoginPage() {
    window.location.href = '/Home/Login'; // Adjust this path if your login page is different
}

// Function to retrieve user details from local storage.
function getUserDetails() {
    try {
        return JSON.parse(localStorage.getItem('UserDetails'));
    } catch (e) {
        console.error("Error parsing UserDetails from localStorage:", e);
        return null;
    }
}

// Function to update the displayed cart item count.
// Requires HTML elements with IDs: #cart-count and #cart-count-mobile.
function updateCartCount() {
    const count = cartItems.reduce((sum, item) => sum + item.quantity, 0);
    $('#cart-count').text(count);
    $('#cart-count-mobile').text(count);
}

// --- Step 1: Call your backend to create a Razorpay Order ---
const createRazorpayOrder = async (itemsInCart) => {
    const token = getAuthToken();
    if (!token) {
        showMessageModal('Authentication Required', 'Please log in to proceed with payment.', false);
        redirectToLoginPage();
        return null;
    }

    try {
        showLoading();
        const response = await fetch(`${API_BASE_URL}/api/Order/CreatePaymentOrder`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify({ cartItems: itemsInCart }) // Send cartItems array directly
        });
        if (!response.ok) {
            const errorData = await response.json();
            // Handle specific status codes if needed
            if (response.status === 401) {
                hideLoading();
                showMessageModal('Unauthorized', 'Your session has expired. Please log in again.', false);
                localStorage.removeItem('jwtToken');
                localStorage.removeItem('UserDetails');
                // You might need to call fetchUserProfile() or similar to update UI
                redirectToLoginPage();
            } else {
                hideLoading();
                showMessageModal('Payment Initiation Failed', errorData.message || 'Could not initiate payment. Please try again.', false);
            }
            return null;
        }

        const data = await response.json();
        console.log("Razorpay Order created response:", data);

        // If the backend handled a free order, it will return a different structure
        if (data.paymentStatus === "Free") {
            hideLoading();
            showMessageModal('Order Placed!', `Your free order #${data.localOrderId} has been placed successfully!`, true);
            cartItems = [];
            localStorage.removeItem('UGroLifeCart');
            updateCartCount();
            $('#cartModal').removeClass('active');
            return { isFreeOrder: true, localOrderId: data.localOrderId }; // Signal that it was a free order
        }

        // For paid orders, the backend now directly processes and returns success in local mode
        // The 'data' object from the backend response already contains the necessary success message
        // and order details for a 'simulated' successful payment.
        return { isFreeOrder: false, ...data }; // Return backend's success data for paid orders
    } catch (error) {
        console.error("Failed to create Razorpay Order:", error);
        showMessageModal('Network Error', 'Could not connect to the server to initiate payment. Please check your internet connection and try again.', false);
        return null;
    }
};

// --- Step 2: Open Razorpay Checkout Modal (No longer called for paid orders in local dev) ---
// This function is kept for reference if you ever re-enable Razorpay, but it's not
// actively used in the checkout flow for local development as per the new requirement.
const openRazorpayCheckout = (paymentOrderResponse) => {
    const userDetails = getUserDetails(); // Get user details for prefill

    const options = {
        key: paymentOrderResponse.keyId,
        amount: paymentOrderResponse.amount, // Amount in paisa
        currency: paymentOrderResponse.currency,
        name: "UGroLife Store",
        description: `Order ID: ${paymentOrderResponse.localOrderId}`,
        image: "https://example.com/your_logo.png", // Replace with your actual logo URL
        order_id: paymentOrderResponse.razorpayOrderId,
        handler: async function (response) {
            // --- Step 3: Payment success callback - Send to your backend for verification ---
            console.log("Razorpay callback response:", response);
            await verifyPayment(
                paymentOrderResponse.localOrderId,
                response.razorpay_payment_id,
                response.razorpay_order_id,
                response.razorpay_signature
            );
        },
        prefill: {
            name: userDetails?.userName || '', // Use optional chaining
            email: userDetails?.email || '',
            contact: userDetails?.phoneNumber || ''
        },
        notes: {
            // Add any notes you want to pass to Razorpay
            // For example: address: "Customer Shipping Address"
        },
        theme: {
            color: "#3399cc"
        }
    };

    // --- Razorpay Integration Start (Commented for Local Development) ---
    /*
    // Uncomment the following lines when deploying to a live environment
    // or when the Razorpay SDK script is properly loaded.
    const rzp = new Razorpay(options);
    rzp.on('payment.failed', function (response) {
        showMessageModal('Payment Failed', `Reason: ${response.error.description || 'Unknown error'}. Please try again.`, false);
        console.error("Payment Failed:", response.error);
    });
    rzp.open();
    */
    // --- Razorpay Integration End ---

    // --- Local Development Mock for Razorpay (Optional) ---
    // This section is now effectively bypassed by the new checkout button logic.
    console.warn("Razorpay checkout is commented out for local development.");
    console.warn("Simulating a successful payment for order:", paymentOrderResponse.localOrderId);

    const mockRazorpayPaymentId = `pay_mock_${Date.now()}`;
    const mockRazorpaySignature = `sig_mock_${Date.now()}`;

    // This call to verifyPayment will no longer be triggered by openRazorpayCheckout
    // if the checkout button logic directly handles success.
    verifyPayment(
        paymentOrderResponse.localOrderId,
        mockRazorpayPaymentId,
        paymentOrderResponse.razorpayOrderId || `order_mock_${Date.now()}`,
        mockRazorpaySignature
    );

    showMessageModal('Payment Gateway Skipped (Local)', 'Razorpay checkout is disabled for local development. Proceeding as if payment succeeded to update order status.', true);
};

// --- Step 4: Call your backend to verify the payment (still used for actual verification in live) ---
// In local development, this function might be called directly by openRazorpayCheckout if it were used,
// but the main checkout flow now handles success directly after createRazorpayOrder.
const verifyPayment = async (localOrderId, razorpayPaymentId, razorpayOrderId, razorpaySignature) => {
    const token = getAuthToken();
    if (!token) {
        showMessageModal('Authentication Required', 'Please log in to verify payment.', false);
        redirectToLoginPage();
        return;
    }

    try {
        showLoading();
        const response = await fetch(`${API_BASE_URL}/api/Order/VerifyPayment`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify({
                localOrderId: localOrderId,
                razorpayPaymentId: razorpayPaymentId,
                razorpayOrderId: razorpayOrderId,
                razorpaySignature: razorpaySignature
            })
        });

        if (!response.ok) {
            const errorData = await response.json();
            hideLoading();
            showMessageModal('Payment Verification Failed', errorData.message || 'An issue occurred during payment verification.', false);
            return;
        }
        hideLoading();
        const data = await response.json();
        showMessageModal('Payment Successful!', data.message || `Your order #${localOrderId} has been processed.`, true);
        console.log("Payment Verification Success:", data);

        // Clear cart and update UI after successful payment
        cartItems = [];
        localStorage.removeItem('UGroLifeCart');
        updateCartCount();
        $('#cartModal').removeClass('active');
        // Optionally redirect to an order confirmation page
        // window.location.href = `/order-confirmation.html?orderId=${localOrderId}`;

    } catch (error) {
        hideLoading();
        console.error("Failed to verify payment:", error);
        showMessageModal('Network Error', 'Could not connect to the server for payment verification. Please try again.', false);
    }
};


$(document).ready(function () {
    let cartItems = JSON.parse(localStorage.getItem('UGroLifeCart')) || [];
     // Ensure this matches your backend API URL
    const userDetailsString = localStorage.getItem('UserDetails');

    // Helper function to decode JWT (simplistic, for display purposes)
    function decodeJwt(token) {
        try {
            const base64Url = token.split('.')[1];
            const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
            const jsonPayload = decodeURIComponent(atob(base64).split('').map(function (c) {
                return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
            }).join(''));
            return JSON.parse(jsonPayload);
        } catch (e) {
            console.error("Error decoding JWT:", e);
            return null;
        }
    }

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

    $('#viewMyOrdersBtnDesktop').on('click', function () {
        window.location.href = '/Home/Orders';
    });
    // Function to fetch and display user profile (or decode from token)
    function fetchUserProfile() {
        if (userDetailsString) {
            const userDetails = JSON.parse(userDetailsString);
            const username = userDetails.fullName || 'User';
            const email = userDetails.email || 'N/A';
            $('#profile-name-desktop').text(username);
            $('#dropdown-username-desktop').text(username);
            $('#dropdown-email-desktop').text(email);

            // Hide login/signup links if logged in
            $('#login-link-desktop').hide(); // Assuming these IDs exist
            $('#signup-link-desktop').hide(); // Assuming these IDs exist
            $('#profile-area-desktop').show(); // Ensure profile area is visible
            $('#logout-button-desktop').show(); // Ensure logout is visible

            $('#login-link-mobile-alt').hide();
            $('#signup-link-mobile-alt').hide();
            $('#profile-area-mobile').show(); // Ensure mobile profile area is visible
            $('#logout-button-mobile').show(); // Ensure mobile logout is visible

            // Ensure "View My Orders" buttons are visible if user is logged in
            $('#viewMyOrdersBtnDesktop').show(); // Specific ID for desktop button
            $('#viewMyOrdersBtnMobile').show(); // Specific ID for mobile button
        } else {
            // If not logged in, show login/signup and hide profile sections
            $('#profile-name-desktop').text('Guest');
            $('#dropdown-username-desktop').text('Guest');
            $('#dropdown-email-desktop').text('');
            $('#profile-name-mobile').text('Guest Profile');
            $('#dropdown-username-mobile').text('Guest');
            $('#dropdown-email-mobile').text('');

            // Ensure login/signup links are visible if logged out
            $('#login-link-desktop').show();
            $('#signup-link-desktop').show();
            $('#profile-area-desktop').hide(); // Hide profile area if not logged in
            $('#logout-button-desktop').hide(); // Hide logout button

            $('#login-link-mobile-alt').show();
            $('#signup-link-mobile-alt').show();
            $('#profile-area-mobile').hide(); // Hide mobile profile area
            $('#logout-button-mobile').hide(); // Hide mobile logout

            // Hide "View My Orders" buttons if user is logged out
            $('#viewMyOrdersBtnDesktop').hide();
            $('#viewMyOrdersBtnMobile').hide();
        }
    }

    // Call fetchUserProfile on page load
    fetchUserProfile();

    // Removed the old #viewMyOrdersBtn click handler as it's now a redirect link

    // Profile dropdown toggling
    $('#profile-icon-desktop, #profile-icon-mobile').on('click', function () {
        const dropdownId = $(this).attr('id').includes('desktop') ? '#profile-dropdown-desktop' : '#profile-dropdown-mobile';
        $(dropdownId).toggleClass('active');
    });

    // Close dropdown when clicking outside
    $(document).on('click', function (event) {
        if (!$(event.target).closest('.profile-dropdown-container').length &&
            !$(event.target).closest('#mobile-menu-button').length) { // Also exclude mobile menu button
            $('#profile-dropdown-desktop').removeClass('active');
            $('#profile-dropdown-mobile').removeClass('active');
            // If mobile menu is open, it might also need to close depending on design
            // $('#mobile-menu').addClass('hidden');
        }
    });

    // Logout button functionality
    $('#logout-button-desktop, #logout-button-mobile').on('click', function () {
        localStorage.removeItem('jwtToken');
        localStorage.removeItem('UserDetails');
        showMessageModal('Logged Out', 'You have been successfully logged out.', true);
        fetchUserProfile(); // Update UI
        window.location.href = '/Home/Login'; // Go back to home page
    });

    // Mobile menu toggle
    $('#mobile-menu-button').on('click', function () {
        $('#mobile-menu').toggleClass('hidden');
    });

    // Filter buttons for products
    $('.filter-btn').on('click', function () {
        $('.filter-btn').removeClass('bg-[#0cae00]').addClass('text-[#111111]');
        $(this).addClass('bg-[#0cae00]').removeClass('text-[#111111]');

        const filter = $(this).data('filter');
        $('.dark-card').each(function () {
            if (filter === 'all' || $(this).data('category') === filter) {
                $(this).show();
            } else {
                $(this).hide();
            }
        });
    }).first().click(); // Activate 'All' button by default

    // Add to cart functionality
    $('.add-to-cart-btn').on('click', function () {
        const productId = $(this).data('product-id');
        const productName = $(this).data('product-name');
        const productPrice = $(this).data('product-price');

        const existingItem = cartItems.find(item => item.id === productId);

        if (existingItem) {
            existingItem.quantity++;
        } else {
            cartItems.push({
                id: productId,
                name: productName,
                price: productPrice,
                quantity: 1
            });
        }
        localStorage.setItem('UGroLifeCart', JSON.stringify(cartItems));
        updateCartCount();
        showMessageModal('Item Added', `${productName} added to cart!`, true);
    });

    // Update cart count display
    function updateCartCount() {
        const count = cartItems.reduce((sum, item) => sum + item.quantity, 0);
        $('#cart-count').text(count);
        $('#cart-count-mobile').text(count);
    }

    // Call updateCartCount on page load to reflect saved cart items
    updateCartCount();

    // Show cart modal
    $('#cart-link, #cart-link-mobile').on('click', function (e) {
        e.preventDefault();
        displayCartItems();
        $('#cartModal').addClass('active');
    });

    // Close cart modal
    $('#closeCartModal').on('click', function () {
        $('#cartModal').removeClass('active');
    });

    // Display items in cart modal
    function displayCartItems() {
        const cartItemsContainer = $('#cartItemsContainer');
        cartItemsContainer.empty();
        let totalAmount = 0;

        if (cartItems.length === 0) {
            cartItemsContainer.append('<p class="text-center text-gray-700">Your cart is empty.</p>');
            $('#checkoutButton').prop('disabled', true);
        } else {
            $('#checkoutButton').prop('disabled', false);
            cartItems.forEach(item => {
                const itemTotal = item.quantity * item.price;
                totalAmount += itemTotal;
                const itemHtml = `
                                                    <div class="flex justify-between items-center border-b border-gray-200 pb-2 text-gray-800">
                                                        <span>${item.name} (₹${item.price.toFixed(2)}) x ${item.quantity}</span>
                                                        <div class="flex items-center space-x-2">
                                                            <button class="remove-from-cart-btn text-red-500 hover:text-red-700 font-bold" data-product-id="${item.id}">-</button>
                                                            <span class="font-semibold">₹${itemTotal.toFixed(2)}</span>
                                                            <button class="add-one-to-cart-btn text-green-500 hover:text-green-700 font-bold" data-product-id="${item.id}">+</button>
                                                        </div>
                                                    </div>
                                                `;
                cartItemsContainer.append(itemHtml);
            });
        }
        $('#cartTotal').text(`Total: ₹${totalAmount.toFixed(2)}`);
    }

    // Handle quantity change in cart
    $(document).on('click', '.remove-from-cart-btn', function () {
        const productId = $(this).data('product-id');
        const itemIndex = cartItems.findIndex(item => item.id === productId);

        if (itemIndex > -1) {
            if (cartItems[itemIndex].quantity > 1) {
                cartItems[itemIndex].quantity--;
            } else {
                cartItems.splice(itemIndex, 1);
            }
            localStorage.setItem('UGroLifeCart', JSON.stringify(cartItems));
            updateCartCount();
            displayCartItems();
        }
    });

    $(document).on('click', '.add-one-to-cart-btn', function () {
        const productId = $(this).data('product-id');
        const item = cartItems.find(item => item.id === productId);
        if (item) {
            item.quantity++;
            localStorage.setItem('UGroLifeCart', JSON.stringify(cartItems));
            updateCartCount();
            displayCartItems();
        }
    });

    function redirectToLoginPage() {
        window.location.href = '/Home/Login'; // Adjust this path if your login page is different
    }
    // Checkout functionality
    const getAuthToken = () => localStorage.getItem('jwtToken');

    // Helper function to get user details from local storage (needed for Razorpay prefill)
    const getUserDetails = () => {
        try {
            return JSON.parse(localStorage.getItem('UserDetails'));
        } catch (e) {
            console.error("Error parsing UserDetails from localStorage:", e);
            return null;
        }
    };
   
    $('#checkoutButton').on('click', async function () {
        const token = getAuthToken();
        if (!token) {
            showMessageModal('Authentication Required', 'Please log in to proceed with your order.', false);
            $('#cartModal').removeClass('active');
            redirectToLoginPage();
            return; // Stop execution
        }

        if (cartItems.length === 0) {
            showMessageModal('Cart Empty', 'Your cart is empty. Please add items before checking out.', false);
            return; // Stop execution
        }

        // --- Show loading indicator ---
        showLoading();

        let paymentResponse = null; // Initialize paymentResponse outside the try block

        try {
            paymentResponse = await createRazorpayOrder(cartItems);

            if (paymentResponse) {
                // For both free and paid orders (in local dev, paid orders are also
                // immediately 'successful' from the backend's perspective)
                showMessageModal('Order Placed!', `Your order #${paymentResponse.localOrderId} has been processed successfully!`, true);
                cartItems = [];
                localStorage.removeItem('UGroLifeCart');
                updateCartCount();
                $('#cartModal').removeClass('active');
            }
            // If paymentResponse is null, an error modal was already shown by createRazorpayOrder
        } catch (error) {
            console.error("Checkout process failed:", error);
            // Optionally, show a more generic error message if createRazorpayOrder didn't handle it
            if (!paymentResponse) { // Only show if createRazorpayOrder didn't already display a specific error
                showMessageModal('Order Error', 'An unexpected error occurred during checkout. Please try again.', false);
            }
        } finally {
            // --- Hide loading indicator regardless of success or failure ---
            hideLoading();
        }
    });


    // Contact Form Submission
    $('#contact-form').on('submit', function (e) {
        e.preventDefault(); // Prevent default form submission

        const name = $('#name').val();
        const email = $('#email').val();
        const message = $('#message').val();
        const mobile = $('#phone').val();
        // Simple validation (can be more robust)
        if (!name || !email || !message || !mobile) {
            showMessageModal('Error', 'Please fill in all fields.');
            return;
        }
        showLoading();
        // Send data to your backend API
        fetch(`${API_BASE_URL}/api/contact/send-email`, { // Create this endpoint on your backend
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                name: name,
                email: email,
                mobile: mobile,
                message: message
            })
        })
            .then(response => {
                if (!response.ok) {
                    // Handle server-side errors
                    hideLoading();
                    return response.json().then(errorData => {
                        throw new Error(errorData.message || 'Failed to send message.');
                    });
                }
                return response.json();
            })
            .then(data => {
                hideLoading();
                showMessageModal('Message Sent!', 'Thank you for your message. We will get back to you shortly.');
                $('#contact-form')[0].reset(); // Clear the form
            })
            .catch(error => {
                hideLoading();
                console.error('Error sending contact form:', error);
                showMessageModal('Error', `Failed to send your message: ${error.message}`);
            });
       
    });
});

