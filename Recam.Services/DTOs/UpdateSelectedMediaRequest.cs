namespace Recam.Services.DTOs;

public record UpdateSelectedMediaRequest(
    List<int> MediaAssetIds,
    bool MarkAsFinal // 例如 true=最终版, false=只是草稿
);