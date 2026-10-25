using System.ComponentModel.DataAnnotations;

namespace HomePlant.ViewModels;

public class FeedbackSubmitViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên của bạn")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Tên từ 2 đến 80 ký tự")]
    public string UserName { get; set; } = "";

    [StringLength(100, ErrorMessage = "Thông tin không quá 100 ký tự")]
    public string UserSubtitle { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng chọn chủ đề chia sẻ")]
    public string Tag { get; set; } = "CỨU CÂY THÀNH CÔNG";

    public string TagIcon { get; set; } = "🌿";

    public string TagType { get; set; } = "success";

    [Range(1, 5, ErrorMessage = "Số sao đánh giá từ 1 đến 5 sao")]
    public int Rating { get; set; } = 5;

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề ngắn gọn")]
    [StringLength(120, MinimumLength = 4, ErrorMessage = "Tiêu đề từ 4 đến 120 ký tự")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng chia sẻ cảm nhận thực tế của bạn")]
    [StringLength(1200, MinimumLength = 15, ErrorMessage = "Nội dung chia sẻ từ 15 đến 1200 ký tự")]
    public string Content { get; set; } = "";
}
