namespace HdLabs.Memo.Helpers;

/// <summary>
/// BlockUIContainer 안 이미지 8방향 핸들. 값 0~7은 핸들 배열 인덱스·Border Tag 정수와 같다.
/// (private 중첩 enum은 FlowDocument XAML에 넣을 수 없어서 네임스페이스에 둔다.)
/// </summary>
public enum MemoImageResizeHandle
{
    Nw, N, Ne, W, E, Sw, S, Se,
}
