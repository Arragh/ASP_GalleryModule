using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASP_GalleryModule.Models.Gallery
{
    public class GalleryImage
    {
        public Guid Id { get; set; }
        public string ImageName { get; set; }
        public string ImagePathNormal { get; set; }
        public string ImagePathScaled { get; set; }
        public string ImageDescription { get; set; }
        public DateTime ImageDate { get; set; }
        public Guid GalleryId { get; set; }
    }
}
