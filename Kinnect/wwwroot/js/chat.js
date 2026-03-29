import { formatChatTimestamp, scrollElementToBottom } from './utils.js';

const PAGE_SIZE = 50;

class ChatRoomRow {
    constructor(data) {
        this.id = ko.observable(data.id);
        this.name = ko.observable(data.name);
        this.adminUserId = ko.observable(data.adminUserId);
        this.unreadCount = ko.observable(0);
    }
}

class ChatMessageRow {
    constructor(data, myUserId) {
        this.id = ko.observable(data.id);
        this.content = ko.observable(data.content);
        this.fromUserId = ko.observable(data.fromUserId);
        this.fromUserName = ko.observable(data.fromUserName || '');
        this.fromFullName = ko.observable(data.fromFullName || data.fromUserName || '?');
        this.timestamp = ko.observable(data.timestamp);
        this.timestampDisplay = ko.pureComputed(() => formatChatTimestamp(this.timestamp()));
        this.isMine = ko.observable(data.fromUserId === myUserId);
    }
}

class OnlineUserRow {
    constructor(data) {
        this.userId = ko.observable(data.userId);
        this.userName = ko.observable(data.userName || '');
        this.fullName = ko.observable(data.fullName || data.userName || '?');
        this.currentRoom = ko.observable(data.currentRoom || '');
        this.personId = ko.observable(data.personId || null);
    }
}

class PrivateConversationRow {
    constructor(userId, fullName) {
        this.userId = ko.observable(userId);
        this.fullName = ko.observable(fullName || userId);
        this.unreadCount = ko.observable(0);
    }
}

class ChatProfileInfo {
    constructor(data) {
        this.userId = ko.observable(data.userId);
        this.userName = ko.observable(data.userName || '');
        this.fullName = ko.observable(data.fullName || data.userName || '?');
        this.isAdmin = ko.observable(!!data.isAdmin);
    }
}

class ChatViewModel {
    constructor(hub) {
        this.hub = hub;

        this.isLoading = ko.observable(true);
        this.serverInfoMessage = ko.observable('');
        this.myProfile = ko.observable(null);
        this.myUserId = ko.observable(null);
        this.isGlobalAdmin = ko.pureComputed(() => this.myProfile() ? !!this.myProfile().isAdmin() : false);

        this.chatRooms = ko.observableArray([]);
        this.chatMessages = ko.observableArray([]);
        this.onlineUsers = ko.observableArray([]);
        this.privateConversations = ko.observableArray([]);
        this.privateMessages = ko.observableArray([]);

        this.activeRoomId = ko.observable(null);
        this.activeRoomName = ko.observable(null);
        this.activePrivateUserId = ko.observable(null);
        this.activePrivateUserName = ko.observable(null);

        this.messageText = ko.observable('');
        this.privateMessageText = ko.observable('');

        this.selectedOnlineUser = ko.observable(null);

        // Infinite scroll state
        this._roomOldestId = null;
        this._roomHasMore = true;
        this._loadingMoreRoom = false;
        this._privateOldestId = null;
        this._privateHasMore = true;
        this._loadingMorePrivate = false;

        this.isRoomAdmin = ko.pureComputed(() => {
            const room = this.chatRooms().find(r => r.id() === this.activeRoomId());
            return !!(room && this.myUserId() && room.adminUserId() === this.myUserId());
        });

        this.isAnnouncementsRoom = ko.pureComputed(() => (this.activeRoomName() || '') === 'Announcements');
        this.canPostToActiveRoom = ko.pureComputed(() => !this.isAnnouncementsRoom() || this.isGlobalAdmin());
        this.canManageActiveRoom = ko.pureComputed(() => this.isRoomAdmin() && !this.isAnnouncementsRoom());
    }

    joinRoom = (room) => {
        this.activePrivateUserId(null);
        this.activePrivateUserName(null);
        this.chatMessages([]);
        this.activeRoomId(room.id());
        this.activeRoomName(room.name());

        // Clear room unread badge
        room.unreadCount(0);

        this.hub.invoke('Join', room.name()).catch(console.error);
        this.loadRoomMessages(room.id());
    };

    loadRoomMessages = async (roomId, beforeId = null) => {
        const container = document.getElementById('chat-messages-room');
        try {
            const url = beforeId
                ? `/api/chat-messages/room/${roomId}?take=${PAGE_SIZE}&beforeId=${beforeId}`
                : `/api/chat-messages/room/${roomId}?take=${PAGE_SIZE}`;
            const res = await fetch(url);
            const data = await res.json();
            const myId = this.myUserId();
            const newMessages = (data || []).map(m => new ChatMessageRow(m, myId));

            if (beforeId) {
                // Prepend older messages and preserve scroll position
                const prevHeight = container ? container.scrollHeight : 0;
                this.chatMessages([...newMessages, ...this.chatMessages()]);
                if (container) {
                    // Keep the viewport at the same relative position
                    container.scrollTop = container.scrollHeight - prevHeight;
                }
                this._roomHasMore = newMessages.length >= PAGE_SIZE;
            } else {
                this.chatMessages(newMessages);
                this._roomOldestId = newMessages.length > 0 ? newMessages[0].id() : null;
                this._roomHasMore = newMessages.length >= PAGE_SIZE;
                setTimeout(() => scrollElementToBottom('chat-messages-room'), 50);
            }

            if (newMessages.length > 0 && !beforeId) {
                this._roomOldestId = newMessages[0].id();
            } else if (newMessages.length > 0 && beforeId) {
                this._roomOldestId = newMessages[0].id();
            }
        } catch (err) {
            console.error('Error loading room messages:', err);
        } finally {
            this._loadingMoreRoom = false;
        }
    };

    loadMoreRoomMessages = async () => {
        if (this._loadingMoreRoom || !this._roomHasMore || this.activeRoomId() == null) return;
        this._loadingMoreRoom = true;
        await this.loadRoomMessages(this.activeRoomId(), this._roomOldestId);
    };

    createRoom = async () => {
        const input = document.getElementById('new-room-name');
        const name = input?.value.trim() ?? '';
        if (!name) return;
        try {
            await fetch('/api/chat-rooms', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name })
            });
        } catch (err) {
            console.error('Error creating room:', err);
        }
        if (input) input.value = '';
    };

    renameRoom = async () => {
        const input = document.getElementById('rename-room-input');
        const name = input?.value.trim() ?? '';
        if (!name || this.activeRoomId() == null) return;
        try {
            await fetch(`/api/chat-rooms/${this.activeRoomId()}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name })
            });
        } catch (err) {
            console.error('Error renaming room:', err);
        }
    };

    deleteRoom = async () => {
        if (this.activeRoomId() == null) return;
        try {
            await fetch(`/api/chat-rooms/${this.activeRoomId()}`, { method: 'DELETE' });
        } catch (err) {
            console.error('Error deleting room:', err);
        }
    };

    sendRoomMessage = async () => {
        if (!this.canPostToActiveRoom()) return;
        const text = this.messageText().trim();
        if (!text || this.activeRoomId() == null) return;
        try {
            await fetch('/api/chat-messages/room', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ roomId: this.activeRoomId(), content: text })
            });
        } catch (err) {
            console.error('Error sending message:', err);
        }
        this.messageText('');
    };

    onEnterKey = (data, event) => {
        if (event.key !== 'Enter') return true;
        event.preventDefault();
        this.sendRoomMessage();
        return false;
    };

    deleteMessage = async () => {
        const hidden = document.getElementById('delete-message-id');
        const id = parseInt(hidden?.value ?? '', 10);
        if (!id) return;
        try {
            await fetch(`/api/chat-messages/${id}`, { method: 'DELETE' });
        } catch (err) {
            console.error('Error deleting message:', err);
        }
    };

    openPrivateChat = (conv) => {
        this.activeRoomId(null);
        this.activeRoomName(null);
        this.chatMessages([]);
        this.privateMessages([]);
        this.activePrivateUserId(conv.userId());
        this.activePrivateUserName(conv.fullName());

        // Clear unread badge and notify server
        if (conv.unreadCount() > 0) {
            if (typeof window.decrementMessageBadge === 'function') {
                window.decrementMessageBadge(conv.unreadCount());
            }
            conv.unreadCount(0);
            fetch(`/api/notifications/mark-read/${encodeURIComponent(conv.userId())}`, { method: 'POST' })
                .catch(console.error);
        }

        this.loadPrivateMessages(conv.userId());
    };

    loadPrivateMessages = async (otherUserId, beforeId = null) => {
        const container = document.getElementById('chat-messages-private');
        try {
            const url = beforeId
                ? `/api/chat-messages/private/${otherUserId}?take=${PAGE_SIZE}&beforeId=${beforeId}`
                : `/api/chat-messages/private/${otherUserId}?take=${PAGE_SIZE}`;
            const res = await fetch(url);
            const data = await res.json();
            const myId = this.myUserId();
            const newMessages = (data || []).map(m => new ChatMessageRow(m, myId));

            if (beforeId) {
                const prevHeight = container ? container.scrollHeight : 0;
                this.privateMessages([...newMessages, ...this.privateMessages()]);
                if (container) {
                    container.scrollTop = container.scrollHeight - prevHeight;
                }
                this._privateHasMore = newMessages.length >= PAGE_SIZE;
            } else {
                this.privateMessages(newMessages);
                this._privateHasMore = newMessages.length >= PAGE_SIZE;
                setTimeout(() => scrollElementToBottom('chat-messages-private'), 50);
            }

            if (newMessages.length > 0) {
                this._privateOldestId = newMessages[0].id();
            }
        } catch (err) {
            console.error('Error loading private messages:', err);
        } finally {
            this._loadingMorePrivate = false;
        }
    };

    loadMorePrivateMessages = async () => {
        if (this._loadingMorePrivate || !this._privateHasMore || !this.activePrivateUserId()) return;
        this._loadingMorePrivate = true;
        await this.loadPrivateMessages(this.activePrivateUserId(), this._privateOldestId);
    };

    sendPrivateMessage = async () => {
        const text = this.privateMessageText().trim();
        const toUserId = this.activePrivateUserId();
        if (!text || !toUserId) return;
        try {
            await this.hub.invoke('SendPrivate', toUserId, text);
        } catch (err) {
            console.error('Error sending private message:', err);
        }
        this.privateMessageText('');
    };

    onPrivateEnterKey = (data, event) => {
        if (event.key !== 'Enter') return true;
        event.preventDefault();
        this.sendPrivateMessage();
        return false;
    };

    startPrivateChatWith = (userId, fullName) => {
        let conv = this.privateConversations().find(c => c.userId() === userId);
        if (!conv) {
            conv = new PrivateConversationRow(userId, fullName);
            this.privateConversations.push(conv);
        }
        this.openPrivateChat(conv);
    };

    // Called from the online users panel dropdown
    startPrivateChatWithUser = (user, event) => {
        event?.preventDefault();
        event?.stopPropagation();
        this.selectedOnlineUser(null);
        this.startPrivateChatWith(user.userId(), user.fullName());
        return false;
    };

    /** Primary click: programmatic nav so Bootstrap dropdown handlers do not swallow the default. Keep href for middle-click / modified clicks. */
    openOnlineUserProfile = (user, event) => {
        if (event && event.button !== 0)
            return true;
        if (event && (event.ctrlKey || event.metaKey || event.shiftKey))
            return true;

        event?.preventDefault();
        event?.stopPropagation();
        const id = user.personId();
        if (id != null)
            window.location.assign(`/Profile/View/${id}`);
        return false;
    };

    // Toggle the action menu for an online user
    selectOnlineUser = (user, event) => {
        event?.stopPropagation();
        // Clicks on the menu (e.g. Send Message) bubble here; ignore so we do not re-select after closing.
        if (event?.target?.closest?.('.chat-user-menu'))
            return;
        this.selectedOnlineUser(this.selectedOnlineUser() === user ? null : user);
    };

    loadInitialData = async (initialPrivateUserId, initialPrivateUserName) => {
        try {
            const [roomsRes, convsRes] = await Promise.all([
                fetch('/api/chat-rooms'),
                fetch('/api/chat-messages/private-conversations')
            ]);
            const rooms = await roomsRes.json();
            const conversations = await convsRes.json();

            this.chatRooms((rooms || []).map(d => new ChatRoomRow(d)));

            // Populate existing private conversations (most recent first)
            for (const conv of (conversations || [])) {
                if (!this.privateConversations().some(c => c.userId() === conv.userId)) {
                    this.privateConversations.push(new PrivateConversationRow(conv.userId, conv.displayName));
                }
            }

            // Apply persisted unread notification badges from the layout's prefetched data
            this._applyPersistedNotifications();

            if (initialPrivateUserId) {
                this.startPrivateChatWith(initialPrivateUserId, initialPrivateUserName || initialPrivateUserId);
            } else if (this.chatRooms().length > 0) {
                this.joinRoom(this.chatRooms()[0]);
            }
        } catch (err) {
            console.error('Failed to load initial data:', err);
            this.onError('Failed to load chat data.');
        }
    };

    _applyPersistedNotifications = () => {
        const data = window._notifData || [];
        for (const entry of data) {
            if (!entry.fromUserId || !entry.unreadCount) continue;
            let conv = this.privateConversations().find(c => c.userId() === entry.fromUserId);
            if (!conv) {
                conv = new PrivateConversationRow(entry.fromUserId, entry.fromDisplayName || entry.fromUserId);
                this.privateConversations.push(conv);
            }
            conv.unreadCount(entry.unreadCount);
        }
    };

    onGetProfileInfo = (data) => {
        this.myProfile(new ChatProfileInfo(data));
        this.myUserId(data.userId);
        this.isLoading(false);
    };

    onNewMessage = (data) => {
        if (this.activeRoomId() === data.toRoomId) {
            this.chatMessages.push(new ChatMessageRow(data, this.myUserId()));
            setTimeout(() => scrollElementToBottom('chat-messages-room'), 30);
        } else {
            // Increment the unread badge on the room that received the message
            const room = this.chatRooms().find(r => r.id() === data.toRoomId);
            if (room) room.unreadCount(room.unreadCount() + 1);
        }
    };

    onNewPrivateMessage = (data) => {
        const me = this.myUserId();
        const otherId = data.fromUserId === me ? data.toUserId : data.fromUserId;
        const otherName = data.fromUserId === me
            ? (this.activePrivateUserName() || otherId)
            : (data.fromFullName || data.fromUserName || otherId);

        let conv = this.privateConversations().find(c => c.userId() === otherId);
        if (!conv) {
            conv = new PrivateConversationRow(otherId, otherName);
            this.privateConversations.push(conv);
        }

        if (this.activePrivateUserId() === otherId) {
            this.privateMessages.push(new ChatMessageRow(data, me));
            setTimeout(() => scrollElementToBottom('chat-messages-private'), 30);

            // The conversation is open: mark the new notification read immediately
            if (data.fromUserId !== me) {
                fetch(`/api/notifications/mark-read/${encodeURIComponent(data.fromUserId)}`, { method: 'POST' })
                    .catch(console.error);
            }
        } else if (data.fromUserId !== me) {
            // Message arrived for a conversation that is not currently open
            conv.unreadCount(conv.unreadCount() + 1);
        }
    };

    // Replace the entire online users list (sent on connect with current users)
    onSetOnlineUsers = (users) => {
        const myId = this.myUserId();
        this.onlineUsers((users || [])
            .filter(u => u.userId !== myId)
            .map(u => new OnlineUserRow(u)));
    };

    onAddUser = (data) => {
        // Don't add self
        if (data.userId === this.myUserId()) return;
        if (!this.onlineUsers().some(u => u.userId() === data.userId))
            this.onlineUsers.push(new OnlineUserRow(data));
    };

    onRemoveUser = (data) => {
        this.onlineUsers.remove(u => u.userId() === data.userId);
    };

    onAddChatRoom = (data) => {
        if (!this.chatRooms().some(r => r.id() === data.id))
            this.chatRooms.push(new ChatRoomRow(data));
    };

    onUpdateChatRoom = (data) => {
        const room = this.chatRooms().find(r => r.id() === data.id);
        if (room) room.name(data.name);
        if (this.activeRoomId() === data.id) this.activeRoomName(data.name);
    };

    onRemoveChatRoom = (id) => {
        this.chatRooms.remove(r => r.id() === id);
        if (this.activeRoomId() === id) {
            this.activeRoomId(null);
            this.activeRoomName(null);
            this.chatMessages([]);
            const first = this.chatRooms()[0];
            if (first) this.joinRoom(first);
        }
    };

    onRemoveChatMessage = (id) => {
        this.chatMessages.remove(m => m.id() === id);
    };

    onError = (msg) => {
        this.serverInfoMessage(msg);
        const el = document.getElementById('chat-error-alert');
        if (el) {
            el.classList.remove('d-none');
            el.style.display = '';
            setTimeout(() => el.classList.add('d-none'), 5000);
        }
    };
}

document.addEventListener('DOMContentLoaded', async () => {
    const hub = new signalR.HubConnectionBuilder()
        .withUrl('/chatHub')
        .withAutomaticReconnect()
        .build();

    const vm = new ChatViewModel(hub);
    ko.applyBindings(vm);

    hub.on('getProfileInfo', vm.onGetProfileInfo);
    hub.on('setOnlineUsers', vm.onSetOnlineUsers);
    hub.on('newMessage', vm.onNewMessage);
    hub.on('newPrivateMessage', vm.onNewPrivateMessage);
    hub.on('addUser', vm.onAddUser);
    hub.on('removeUser', vm.onRemoveUser);
    hub.on('addChatRoom', vm.onAddChatRoom);
    hub.on('updateChatRoom', vm.onUpdateChatRoom);
    hub.on('removeChatRoom', vm.onRemoveChatRoom);
    hub.on('removeChatMessage', vm.onRemoveChatMessage);
    hub.on('onRoomDeleted', () => {
        const first = vm.chatRooms()[0];
        if (first) vm.joinRoom(first);
        else {
            vm.activeRoomId(null);
            vm.activeRoomName(null);
            vm.chatMessages([]);
        }
    });
    hub.on('onError', vm.onError);

    const initialUserId = typeof chatInitialPrivateUserId !== 'undefined' ? chatInitialPrivateUserId : null;
    const initialUserName = typeof chatInitialPrivateUserName !== 'undefined' ? chatInitialPrivateUserName : null;

    try {
        await hub.start();
        if (vm.isLoading()) vm.isLoading(false);
        await vm.loadInitialData(initialUserId, initialUserName);
    } catch (err) {
        console.error('SignalR connection error:', err);
        vm.onError('SignalR connection failed.');
    }

    // Clear nav-bar notification badge now that we are on the chat page
    if (typeof window.clearMessageBadge === 'function') {
        window.clearMessageBadge();
    }

    // ── Infinite scroll ──────────────────────────────────────────────────────
    const setupInfiniteScroll = (elementId, loader) => {
        const el = document.getElementById(elementId);
        if (!el) return;
        el.addEventListener('scroll', () => {
            if (el.scrollTop === 0) loader();
        });
    };

    setupInfiniteScroll('chat-messages-room', () => vm.loadMoreRoomMessages());
    setupInfiniteScroll('chat-messages-private', () => vm.loadMorePrivateMessages());

    // ── Modal helpers ────────────────────────────────────────────────────────
    document.addEventListener('show.bs.modal', (e) => {
        if (e.target.id === 'remove-message-modal') {
            const msgId = e.relatedTarget?.getAttribute('data-message-id');
            const hidden = document.getElementById('delete-message-id');
            if (hidden && msgId) hidden.value = msgId;
        }
        if (e.target.id === 'rename-room-modal') {
            const input = document.getElementById('rename-room-input');
            if (input) input.value = vm.activeRoomName() || '';
        }
    });

    // Close the user action menu when clicking anywhere outside
    document.addEventListener('click', () => {
        if (vm.selectedOnlineUser()) vm.selectedOnlineUser(null);
    });

    document.getElementById('expand-users-list')?.addEventListener('click', () => {
        const panel = document.getElementById('chat-users-panel');
        if (panel) panel.style.display = panel.style.display === 'none' ? '' : 'none';
    });
});
