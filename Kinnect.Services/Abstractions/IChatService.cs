namespace Kinnect.Services.Abstractions;

public interface IChatService
{
    Task<Result<ChatMessageDto>> CreatePrivateMessageAsync(string currentUserId, string toUserId, string content);

    Task<Result<ChatRoomDto>> CreateRoomAsync(string name, string currentUserId);

    Task<Result<ChatMessageDto>> CreateRoomMessageAsync(int roomId, string content, string currentUserId, bool isAdmin = false);

    Task<Result<ChatDeleteMessageDto>> DeleteMessageAsync(int messageId, string currentUserId);

    Task<Result<ChatDeleteRoomDto>> DeleteRoomAsync(int roomId, string currentUserId, bool isAdmin = false);

    Task<Result<ChatUserDto>> GetCurrentChatUserAsync(string currentUserId, string fallbackUserName);

    Task<Result<ChatPrivateConversationTargetDto>> GetPrivateConversationTargetAsync(string userId);

    Task<Result<IEnumerable<ChatPrivateConversationTargetDto>>> GetPrivateConversationPartnersAsync(string currentUserId);

    Task<Result<IEnumerable<ChatMessageDto>>> GetPrivateMessagesAsync(string currentUserId, string otherUserId, int take, int? beforeId = null);

    Task<Result<ChatRoomDto>> GetRoomByIdAsync(int roomId);

    Task<Result<IEnumerable<ChatMessageDto>>> GetRoomMessagesAsync(int roomId, int take, int? beforeId = null);

    Task<Result<IEnumerable<ChatRoomDto>>> GetRoomsAsync();

    Task<Result<ChatRoomDto>> UpdateRoomAsync(int roomId, string name, string currentUserId);
}