/**
 * chatSearch.js - Chat and User Search Module
 * Provides local chat search and server-based user search with role-based access
 */

(function () {
    'use strict';

    // Module state
    let searchTimeout = null;
    let currentSearchQuery = '';
    let userRole = '';

    /**
     * Initialize the search module
     */
    function init() {
        console.log('[ChatSearch] Initializing chat search module...');
        
        // Get user role from hidden input
        userRole = checkUserRole();
        console.log('[ChatSearch] User role:', userRole);

        // Setup event listeners
        setupEventListeners();

        // Show/hide user search tab based on role
        updateTabsVisibility();
    }

    /**
     * Check user role from hidden input or claims
     */
    function checkUserRole() {
        const roleInput = document.getElementById('userRole');
        if (roleInput && roleInput.value) {
            return roleInput.value;
        }

        // Fallback: try to get from user claims if available
        // This would need to be set in the Index.cshtml
        return '';
    }

    /**
     * Setup all event listeners
     */
    function setupEventListeners() {
        const searchInput = document.getElementById('chatSearchInput');
        const clearBtn = document.getElementById('clearSearchBtn');
        const chatsTab = document.getElementById('searchChatsTab');
        const usersTab = document.getElementById('searchUsersTab');

        if (searchInput) {
            searchInput.addEventListener('input', handleSearchInput);
            searchInput.addEventListener('focus', function() {
                // Prevent zoom on iOS
                if (this.style.fontSize !== '16px') {
                    this.style.fontSize = '16px';
                }
            });
        }

        if (clearBtn) {
            clearBtn.addEventListener('click', clearSearch);
        }

        if (chatsTab) {
            chatsTab.addEventListener('click', () => switchTab('chats'));
        }

        if (usersTab) {
            usersTab.addEventListener('click', () => switchTab('users'));
        }
    }

    /**
     * Update tab visibility based on user role
     */
    function updateTabsVisibility() {
        const usersTab = document.getElementById('searchUsersTab');
        const allowedRoles = ['Manager', 'Personel'];

        if (usersTab) {
            if (allowedRoles.includes(userRole)) {
                usersTab.style.display = 'inline-block';
            } else {
                usersTab.style.display = 'none';
                // Ensure chats tab is active if user tab is hidden
                switchTab('chats');
            }
        }
    }

    /**
     * Handle search input with debouncing
     */
    function handleSearchInput(event) {
        const query = event.target.value.trim();
        currentSearchQuery = query;

        // Show/hide clear button
        const clearBtn = document.getElementById('clearSearchBtn');
        if (clearBtn) {
            clearBtn.style.display = query ? 'block' : 'none';
        }

        // Clear previous timeout
        if (searchTimeout) {
            clearTimeout(searchTimeout);
        }

        // If query is empty, show all chats
        if (!query) {
            showAllChats();
            hideSearchResults();
            return;
        }

        // Debounce search (500ms delay)
        searchTimeout = setTimeout(() => {
            performSearch(query);
        }, 500);
    }

    /**
     * Perform search based on active tab
     */
    function performSearch(query) {
        const activeTab = getActiveTab();
        console.log('[ChatSearch] Performing search:', { query, activeTab });

        if (activeTab === 'chats') {
            searchInChats(query);
        } else if (activeTab === 'users') {
            searchUsers(query);
        }
    }

    /**
     * Get the currently active tab
     */
    function getActiveTab() {
        const chatsTab = document.getElementById('searchChatsTab');
        const usersTab = document.getElementById('searchUsersTab');

        if (usersTab && usersTab.classList.contains('active')) {
            return 'users';
        }
        return 'chats';
    }

    /**
     * Switch between tabs
     */
    function switchTab(tab) {
        const chatsTab = document.getElementById('searchChatsTab');
        const usersTab = document.getElementById('searchUsersTab');

        if (tab === 'chats') {
            if (chatsTab) chatsTab.classList.add('active');
            if (usersTab) usersTab.classList.remove('active');
        } else if (tab === 'users') {
            if (usersTab) usersTab.classList.add('active');
            if (chatsTab) chatsTab.classList.remove('active');
        }

        // Perform search with current query
        if (currentSearchQuery) {
            performSearch(currentSearchQuery);
        } else {
            hideSearchResults();
            if (tab === 'chats') {
                showAllChats();
            }
        }
    }

    /**
     * Search in local chats
     */
    function searchInChats(query) {
        console.log('[ChatSearch] Searching in local chats:', query);
        
        const chatList = document.getElementById('chatContactTab');
        if (!chatList) {
            console.error('[ChatSearch] Chat list not found');
            return;
        }

        const chatItems = chatList.querySelectorAll('.chat-box');
        let foundCount = 0;

        chatItems.forEach(chatItem => {
            const chatName = chatItem.querySelector('.name')?.textContent || '';
            const lastMessage = chatItem.querySelector('.msg-detail')?.textContent || '';
            
            const searchText = (chatName + ' ' + lastMessage).toLowerCase();
            const matches = searchText.includes(query.toLowerCase());

            if (matches) {
                chatItem.style.display = '';
                foundCount++;
            } else {
                chatItem.style.display = 'none';
            }
        });

        console.log('[ChatSearch] Found', foundCount, 'matching chats');
        
        // Hide search results container when searching in chats
        hideSearchResults();
    }

    /**
     * Search users from server
     */
    async function searchUsers(query) {
        console.log('[ChatSearch] Searching users on server:', query);

        // Validate query length
        if (query.length < 2) {
            displayUserResults([]);
            showMessage('لطفاً حداقل 2 کاراکتر وارد کنید', 'info');
            return;
        }

        // Show loading state
        showLoading();

        try {
            // Get CSRF token if available
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            const headers = {
                'Content-Type': 'application/json'
            };
            
            // Add CSRF token if available
            if (token) {
                headers['RequestVerificationToken'] = token;
            }

            const response = await fetch(`/api/chat/searchUsers?query=${encodeURIComponent(query)}`, {
                method: 'GET',
                headers: headers,
                credentials: 'same-origin'
            });
            
            if (!response.ok) {
                if (response.status === 403) {
                    throw new Error('شما دسترسی به جستجوی کاربران ندارید');
                } else if (response.status === 401) {
                    throw new Error('لطفاً وارد سیستم شوید');
                }
                throw new Error('خطا در جستجوی کاربران');
            }

            const result = await response.json();
            console.log('[ChatSearch] Search results:', result);

            if (result.success && result.data) {
                displayUserResults(result.data);
            } else {
                displayUserResults([]);
                showMessage('کاربری یافت نشد', 'info');
            }
        } catch (error) {
            console.error('[ChatSearch] Error searching users:', error);
            displayUserResults([]);
            showMessage(error.message || 'خطا در جستجوی کاربران', 'error');
        } finally {
            hideLoading();
        }
    }

    /**
     * Display user search results
     */
    function displayUserResults(users) {
        const resultsContainer = document.getElementById('searchResults');
        if (!resultsContainer) {
            console.error('[ChatSearch] Search results container not found');
            return;
        }

        // Clear previous results
        resultsContainer.innerHTML = '';

        if (users.length === 0) {
            resultsContainer.style.display = 'none';
            return;
        }

        // Show results container
        resultsContainer.style.display = 'block';

        // Hide chat list when showing search results
        const chatList = document.getElementById('chatContactTab');
        if (chatList) {
            chatList.style.display = 'none';
        }

        // Create user result items
        users.forEach(user => {
            const userItem = createUserResultItem(user);
            resultsContainer.appendChild(userItem);
        });
    }

    /**
     * Create a user result item element
     */
    function createUserResultItem(user) {
        const div = document.createElement('div');
        div.className = 'chat-box user-result-item';
        div.style.cursor = 'pointer';
        
        const profilePic = user.profilePicName || 'UserIcon.png';
        const profileImageUrl = `/assets/media/avatar/${profilePic}`;

        div.innerHTML = `
            <div class="d-flex align-items-center gap-2" style="padding: 12px;">
                <div class="flex-shrink-0">
                    <img src="${profileImageUrl}" 
                         alt="${user.nameFamily}" 
                         class="img-fluid rounded-circle"
                         style="width: 50px; height: 50px; object-fit: cover;">
                </div>
                <div class="flex-grow-1 overflow-hidden">
                    <div class="d-flex justify-content-between align-items-center">
                        <h5 class="name mb-1" style="font-size: 14px; font-weight: 600;">
                            ${user.nameFamily}
                        </h5>
                    </div>
                    <div class="d-flex gap-2" style="font-size: 12px; color: #666;">
                        <span class="badge bg-secondary">${user.roleFaName || user.roleName}</span>
                        ${user.deptName ? `<span>${user.deptName}</span>` : ''}
                    </div>
                </div>
            </div>
        `;

        // Add click event to start private chat
        div.addEventListener('click', () => startPrivateChat(user));

        return div;
    }

    /**
     * Start a private chat with a user
     */
    async function startPrivateChat(user) {
        console.log('[ChatSearch] Starting private chat with user:', user.userId);

        try {
            // Check if a private chat already exists with this user
            // If so, open it. If not, the chat will be created when the first message is sent
            // For now, we'll just clear the search and show a message
            
            showMessage(`شروع چت با ${user.nameFamily}...`, 'info');
            
            // Clear search
            clearSearch();

            // TODO: Implement actual chat opening logic
            // This would involve:
            // 1. Check if private chat exists with this user
            // 2. If yes, open the chat
            // 3. If no, prepare to create a new chat when user sends first message
            
            // For now, show a notification using toast system
            showMessage(`قابلیت شروع چت خصوصی با ${user.nameFamily} به زودی فعال می‌شود`, 'info');
            
        } catch (error) {
            console.error('[ChatSearch] Error starting private chat:', error);
            showMessage('خطا در شروع چت', 'error');
        }
    }

    /**
     * Show loading state
     */
    function showLoading() {
        const resultsContainer = document.getElementById('searchResults');
        if (resultsContainer) {
            resultsContainer.style.display = 'block';
            resultsContainer.innerHTML = `
                <div class="text-center p-4">
                    <div class="spinner-border text-primary" role="status">
                        <span class="visually-hidden">در حال جستجو...</span>
                    </div>
                    <p class="mt-2">در حال جستجو...</p>
                </div>
            `;
        }

        // Hide chat list
        const chatList = document.getElementById('chatContactTab');
        if (chatList) {
            chatList.style.display = 'none';
        }
    }

    /**
     * Hide loading state
     */
    function hideLoading() {
        // Loading will be replaced by results or message
    }

    /**
     * Show all chats (clear filter)
     */
    function showAllChats() {
        const chatList = document.getElementById('chatContactTab');
        if (!chatList) return;

        chatList.style.display = '';
        const chatItems = chatList.querySelectorAll('.chat-box');
        chatItems.forEach(chatItem => {
            chatItem.style.display = '';
        });
    }

    /**
     * Hide search results container
     */
    function hideSearchResults() {
        const resultsContainer = document.getElementById('searchResults');
        if (resultsContainer) {
            resultsContainer.style.display = 'none';
            resultsContainer.innerHTML = '';
        }
    }

    /**
     * Clear search
     */
    function clearSearch() {
        const searchInput = document.getElementById('chatSearchInput');
        if (searchInput) {
            searchInput.value = '';
        }

        const clearBtn = document.getElementById('clearSearchBtn');
        if (clearBtn) {
            clearBtn.style.display = 'none';
        }

        currentSearchQuery = '';
        hideSearchResults();
        showAllChats();

        console.log('[ChatSearch] Search cleared');
    }

    /**
     * Show a message to user
     */
    function showMessage(message, type = 'info') {
        console.log(`[ChatSearch] ${type.toUpperCase()}:`, message);
        
        // Use existing toast if available
        if (window.chatUIRenderer && typeof window.chatUIRenderer.showToast === 'function') {
            window.chatUIRenderer.showToast(message, type);
        } else {
            // Fallback to console
            console.log('[ChatSearch] Message:', message);
        }
    }

    /**
     * Public API
     */
    window.chatSearch = {
        init: init,
        clear: clearSearch,
        refresh: showAllChats
    };

    // Auto-initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
