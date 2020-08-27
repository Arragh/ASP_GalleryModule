using ASP_GalleryModule.Models.Gallery;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ASP_GalleryModule.ViewModels.Gallery
{
    public class AddImageViewModel
    {
        public Guid GalleryId { get; set; }

        [Required(ErrorMessage = "Требуется указать путь к файлу.")]
        [Display(Name = "Загрузить изображение")]
        public GalleryImage GalleryImage { get; set; }
    }
}
