using System;
using System.ComponentModel.DataAnnotations;

namespace ASP_GalleryModule.ViewModels.Gallery
{
    public class EditGalleryViewModel
    {
        public Guid GalleryId { get; set; }

        [Required(ErrorMessage = "Требуется ввести заголовок.")]
        [Display(Name = "Заголовок")]
        [StringLength(100, ErrorMessage = "Заголовок должен быть от {2} до {1} символов.", MinimumLength = 4)]
        [DataType(DataType.Text)]
        public string GalleryTitle { get; set; }

        [Required(ErrorMessage = "Требуется ввести краткое описание")]
        [Display(Name = "Краткое описание")]
        [StringLength(1000, ErrorMessage = "Описание должно быть от {2} до {1} символов.", MinimumLength = 4)]
        [DataType(DataType.Text)]
        public string GalleryDescription { get; set; }

        [Display(Name = "Превью-Картинка")]
        public string GalleryPreviewImage { get; set; }
    }
}
