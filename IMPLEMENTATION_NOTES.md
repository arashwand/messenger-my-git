# Private Chats and System Messages Implementation

## Overview
This implementation adds support for private chats and system messages in the messenger application, allowing users to have one-to-one conversations and receive system broadcasts based on their roles.

## Features Implemented

### 1. Database Layer
- **MessageRecipient Table**: Tracks recipients for broadcast messages
  - Columns: MessageRecipientId, MessageId, RecipientUserId, IsRead, ReadDateTime
  - Indexes for efficient querying
  - Foreign keys to Messages and Users tables

### 2. Private Chat Types

#### a. One-to-One Private Chats
- Regular private messages between two users
- Chat key format: `private_{otherUserId}`
- Displays user's profile picture and name
- Shows unread message count

#### b. System Messages
- Broadcast messages to groups of users based on role
- Chat key format: `systemchat_{currentUserId}`
- Displays school icon and school name
- Three broadcast types:
  - **AllStudents**: Messages for all users with Student role
  - **AllTeachers**: Messages for all users with Teacher role
  - **AllPersonel**: Messages for all users with Personnel role
  - **Private with Recipients**: Messages for specific users via MessageRecipients

### 3. API Endpoints

#### GET /api/messages/private-chats
Returns list of private chats for the authenticated user.

**Response**: Array of `PrivateChatItemDto`
```json
[
  {
    "chatId": 123,
    "chatKey": "private_123",
    "chatName": "John Doe",
    "profilePicName": "user123.jpg",
    "lastMessage": {
      "messageId": 456,
      "text": "Hello",
      "senderId": 123,
      "senderName": "John Doe",
      "sentAt": "2025-12-25T10:30:00Z"
    },
    "unreadCount": 5,
    "isSystemChat": false,
    "otherUserId": 123,
    "lastMessageDateTime": "2025-12-25T10:30:00Z"
  },
  {
    "chatId": 1,
    "chatKey": "systemchat_1",
    "chatName": "ایران اروپا",
    "profilePicName": "system_icon",
    "lastMessage": {...},
    "unreadCount": 2,
    "isSystemChat": true,
    "otherUserId": null,
    "lastMessageDateTime": "2025-12-25T09:00:00Z"
  }
]
```

#### GET /api/messages/private-chat/{chatKey}
Returns messages for a specific private chat.

**Parameters**:
- `chatKey`: Chat identifier (e.g., "private_123" or "systemchat_456")
- `pageNumber`: Page number (default: 1)
- `pageSize`: Number of messages per page (default: 50)
- `messageId`: Message ID for cursor-based pagination (default: 0)
- `loadOlder`: Load older messages (default: false)

**Response**: Array of `MessageDto`

### 4. Frontend Components

#### _ClassGroups.cshtml
Updated to display private chats before groups and channels.

**Features**:
- System chat icon: `~/assets/media/shared-photos/03.jpg`
- User profile pictures: `{baseUrl}/uploads/thumb/{chatId}/{profilePicName}`
- Unread badge with count
- Click handler: `GetPrivateChatMessages(chatKey, chatName, lastMessageId)`

#### JavaScript Functions

**GetPrivateChatMessages(chatKey, chatName, lastReadMessageId)**
- Loads messages for a private or system chat
- Calls API: `/api/messages/private-chat/{chatKey}`
- Updates URL history for navigation
- Re-attaches scroll listener for pagination

**renderPrivateChatMessages(messages, chatKey, chatName)**
- Renders messages in the chat panel
- Displays chat header with name
- Shows individual messages with timestamps

### 5. Configuration

#### appsettings.json
Added new configuration section:
```json
{
  "AppSettings": {
    "SchoolName": "ایران اروپا",
    "SystemChatIcon": "~/assets/media/shared-photos/03.jpg"
  }
}
```

### 6. Service Layer

#### MessageService.GetUserPrivateChatsAsync
**Algorithm**:
1. Fetch all private messages where user is sender or recipient
2. Group by other user ID
3. Get last message per conversation
4. Calculate unread count per conversation
5. Fetch system messages based on user role:
   - Check MessageType enum (AllStudents/AllTeachers/AllPersonel)
   - Include messages from MessageRecipients table
6. Create system chat item if system messages exist
7. Sort all chats by last message date (newest first)

#### MessageService.GetPrivateChatMessagesAsync
**Algorithm**:
1. Parse chatKey to determine type:
   - `systemchat_*`: System messages
   - `private_*`: Regular private messages
2. For system messages:
   - Filter by user role
   - Include messages from MessageRecipients
3. For private messages:
   - Filter by sender/receiver pair
4. Apply pagination with messageId cursor
5. Include related data (texts, files, replies, sender info)
6. Return messages ordered by date

## Database Schema

### MessageRecipients Table
```sql
CREATE TABLE MessageRecipients (
    MessageRecipientID BIGINT PRIMARY KEY IDENTITY(1,1),
    MessageID BIGINT NOT NULL,
    RecipientUserID BIGINT NOT NULL,
    IsRead BIT NOT NULL DEFAULT 0,
    ReadDateTime DATETIME NULL,
    CONSTRAINT FK_MessageRecipients_Messages FOREIGN KEY (MessageID) 
        REFERENCES Messages(MessageID) ON DELETE CASCADE,
    CONSTRAINT FK_MessageRecipients_Users FOREIGN KEY (RecipientUserID) 
        REFERENCES Users(UserID)
);

CREATE INDEX IX_MessageRecipients_Message_Recipient 
    ON MessageRecipients(MessageID, RecipientUserID);
CREATE INDEX IX_MessageRecipients_RecipientUserId 
    ON MessageRecipients(RecipientUserID);
```

## Migration

### File: 20251225080000_AddMessageRecipientsTable.cs
Location: `DatabaseMigrationTool/Migrations/`

**To apply migration**:
```bash
cd DatabaseMigrationTool
dotnet ef database update
```

## Usage Examples

### Frontend - Clicking a Private Chat
When user clicks on a private chat in the sidebar:
```javascript
GetPrivateChatMessages('private_123', 'John Doe', 0);
```

### Frontend - Clicking System Chat
When user clicks on system messages:
```javascript
GetPrivateChatMessages('systemchat_456', 'ایران اروپا', 0);
```

### Backend - Sending to Specific Users
To send a system message to specific users, create message with:
- `MessageType = EnumMessageType.Private`
- `IsSystemMessage = true`
- Add entries to `MessageRecipients` table

## Testing Checklist

### Unit Testing (Not Implemented)
- [ ] Test GetUserPrivateChatsAsync with different user roles
- [ ] Test GetPrivateChatMessagesAsync with various chatKey formats
- [ ] Test pagination logic
- [ ] Test unread count calculation

### Integration Testing (Requires Runtime)
- [ ] Test private chat listing API endpoint
- [ ] Test private chat messages API endpoint
- [ ] Test system message filtering by role
- [ ] Verify MessageRecipients filtering

### UI Testing (Requires Runtime)
- [ ] Verify private chats display in sidebar
- [ ] Verify system chat displays with correct icon
- [ ] Test clicking on private chat loads messages
- [ ] Test clicking on system chat loads messages
- [ ] Verify unread badges show correct counts
- [ ] Test profile picture display
- [ ] Test pagination when scrolling

## Future Enhancements

### Redis Integration
Currently uses existing Redis patterns. Can be enhanced:
- Store last message per private chat: `private_chat:{chatKey}:last_message`
- Cache unread counts: `private_chat:{userId}:unread_count`
- Implement real-time updates via SignalR

### Additional Features
- Read receipts for private messages
- Typing indicators
- Message delivery status
- Search within private chats
- Archive/delete conversations
- Block/unblock users
- Message reactions

## Security Considerations

1. **Authorization**: All endpoints require authentication
2. **User Validation**: Service methods verify user existence
3. **Data Filtering**: Users only see their own private chats
4. **Role-Based Access**: System messages filtered by user role
5. **SQL Injection**: Using EF Core parameterized queries
6. **XSS Prevention**: Frontend should sanitize message text

## Performance Considerations

1. **Indexes**: Created on MessageRecipients for efficient queries
2. **Pagination**: Cursor-based pagination with messageId
3. **Eager Loading**: Using Include() for related data
4. **Query Optimization**: Separate queries for private vs system messages
5. **Caching**: Can use Redis for frequently accessed data

## Troubleshooting

### Private chats not showing
- Check if user has any private messages in database
- Verify MessagePrivate records exist
- Check user role for system messages

### System chat not appearing
- Verify IsSystemMessage flag is set
- Check MessageType enum value matches user role
- Ensure MessageRecipients entries exist if using targeted messages

### Profile pictures not loading
- Verify ViewData["baseUrl"] is set in controller
- Check file paths in uploads/thumb directory
- Ensure ProfilePicName column has values

### Unread count incorrect
- Check MessageReads table entries
- Verify userId in MessageReads matches current user
- Ensure messages are properly associated with chats

## Files Modified

### Backend
- Models/Models/MessageRecipient.cs (new)
- Models/Models/Message.cs
- Models/Models/User.cs
- Models/Models/IEMessengerDbContext.cs
- DTOs/PrivateChatItemDto.cs (new)
- Services/Configuration/AppSettings.cs (new)
- Services/Interfaces/IMessageService.cs
- Services/Services/MessageService.cs
- Messenger.API/Controllers/MessagesController.cs
- Messenger.API/appsettings.json

### Frontend
- Messenger.WebApp/Models/ViewModels/UserChatsViewModel.cs (new)
- Messenger.WebApp/Controllers/HomeController.cs
- Messenger.WebApp/Views/Shared/_ClassGroups.cshtml
- Messenger.WebApp/Views/Home/Index.cshtml
- Messenger.WebApp/ServiceHelper/Interfaces/IMessageServiceClient.cs
- Messenger.WebApp/ServiceHelper/MessageServiceClient.cs
- Messenger.WebApp/appsettings.json

### Database
- DatabaseMigrationTool/Migrations/20251225080000_AddMessageRecipientsTable.cs (new)

### Build
- Messenger.sln (fixed invalid project reference)

## Conclusion

This implementation provides a complete solution for private chats and system messages in the messenger application. The code follows existing patterns in the codebase and integrates seamlessly with the current architecture. All backend services, API endpoints, and frontend components are in place and ready for runtime testing once the database migration is applied.
