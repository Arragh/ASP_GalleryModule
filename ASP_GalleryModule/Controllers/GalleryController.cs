﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ASP_GalleryModule.Models.Gallery;
using ASP_GalleryModule.Models.Service;
using ASP_GalleryModule.ViewModels.Gallery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ASP_GalleryModule.Controllers
{
    public class GalleryController : Controller
    {
        CmsContext cmsDB;
        IWebHostEnvironment _appEnvironment;

        public GalleryController(CmsContext context, IWebHostEnvironment appEnvironment)
        {
            cmsDB = context;
            _appEnvironment = appEnvironment;
        }

        #region Главная страница галереи
        public async Task<IActionResult> Index(int pageNumber = 1)
        {
            // Количество записей на страницу
            int pageSize = 12;

            // Формируем список записей для обработки перед выводом на страницу
            IQueryable<Gallery> source = cmsDB.Galleries;

            // Рассчитываем, какие именно записи будут выведены на странице
            List<Gallery> galleries = await source.OrderByDescending(n => n.GalleryDate).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

            // Общее количество записей для дальнейшего рассчета количества страниц
            int galleriesCount = await source.CountAsync();

            // Создаем массив Id-шников записей для выборки изображений к ним
            Guid[] galleryIdArray = galleries.Select(n => n.Id).ToArray();

            // Создаем список из изображений, которые будут поданы вместе со списом записей
            List<GalleryImage> galleryImages = new List<GalleryImage>();

            // Перебираем изображения в БД
            foreach (var image in cmsDB.GalleryImages)
            {
                // Перебираем все элементы ранее созданного массива Guid[] newsIdArray
                foreach (var galleryId in galleryIdArray)
                {
                    // Если данные совпадают, то кладём изображение в список для вывода на странице
                    if (image.GalleryId == galleryId)
                    {
                        galleryImages.Add(image);
                    }
                }
            }

            // Создаём модель для вывода на странице и кладём в неё все необходимые данные
            IndexViewModel model = new IndexViewModel()
            {
                Galleries = galleries,
                GalleryImages = galleryImages,
                CurrentPage = pageNumber,
                TotalPages = (int)Math.Ceiling(galleriesCount / (double)pageSize)
            };

            // Выводим модель в представление
            return View(model);
        }
        #endregion

        #region Создать галерею [GET]
        [HttpGet]
        public IActionResult AddGallery()
        {
            return View();
        }
        #endregion

        #region Создать галерею [POST]
        [HttpPost]
        public async Task<IActionResult> AddGallery(AddGalleryViewModel model)
        {
            if (ModelState.IsValid)
            {
                Gallery gallery = new Gallery()
                {
                    Id = Guid.NewGuid(),
                    GalleryTitle = model.GalleryTitle,
                    GalleryDescription = model.GalleryDescription,
                    GalleryDate = DateTime.Now,
                    UserName = "Mnemonic"
                };

                await cmsDB.Galleries.AddAsync(gallery);
                await cmsDB.SaveChangesAsync();

                return RedirectToAction("Index", "Gallery");
            }

            return View(model);
        }
        #endregion

        #region Просмотр галереи [GET]
        [HttpGet]
        public async Task<IActionResult> ViewGallery(Guid galleryId, string imageToDeleteName = null)
        {
            // Если есть изображение, которое надо удалить, заходим в тело условия
            if (imageToDeleteName != null)
            {
                // Создаем экземпляр класса картинки и присваиваем ему данные из БД
                GalleryImage galleryImage = await cmsDB.GalleryImages.FirstAsync(i => i.ImageName == imageToDeleteName);

                // Делаем еще одну проверку. Лучше перебдеть. Если все ок, заходим в тело условия и удаляем изображения
                if (galleryImage != null)
                {
                    // Исходные (полноразмерные) изображения
                    FileInfo imageNormal = new FileInfo(_appEnvironment.WebRootPath + galleryImage.ImagePathNormal);
                    if (imageNormal.Exists)
                    {
                        imageNormal.Delete();
                    }
                    // И их уменьшенные копии
                    FileInfo imageScaled = new FileInfo(_appEnvironment.WebRootPath + galleryImage.ImagePathScaled);
                    if (imageScaled.Exists)
                    {
                        imageScaled.Delete();
                    }
                    // Удаляем информацию об изображениях из БД и сохраняем
                    cmsDB.GalleryImages.Remove(galleryImage);
                    await cmsDB.SaveChangesAsync();
                }
            }

            // Создаем экземпляр класса News и присваиваем ему значения из БД
            Gallery gallery = await cmsDB.Galleries.FirstAsync(n => n.Id == galleryId);
            // Создаем список изображений из БД, закрепленных за выбранной новостью
            List<GalleryImage> images = new List<GalleryImage>();
            foreach (var image in cmsDB.GalleryImages)
            {
                if (image.GalleryId == galleryId)
                {
                    images.Add(image);
                }
            }

            // Создаем модель для передачи в представление и присваиваем значения
            ViewGalleryViewModel model = new ViewGalleryViewModel()
            {
                GalleryTitle = gallery.GalleryTitle,
                GalleryDescription = gallery.GalleryDescription,
                GalleryImages = images,
                // Скрытые поля
                GalleryId = galleryId,
                GalleryDate = gallery.GalleryDate,
                UserName = gallery.UserName,
                ImagesCount = images.Count
            };
            // Передаем модель в представление
            return View(model);

            //List<GalleryImage> galleryImages = await cmsDB.GalleryImages.Where(i => i.GalleryId == galleryId).ToListAsync();

            //ViewBag.GalleryId = galleryId;
            //ViewBag.GalleryImages = galleryImages;

            //return View();
        }
        #endregion

        #region Просмотр галереи [POST]
        [HttpPost]
        public async Task<IActionResult> ViewGallery(ViewGalleryViewModel model, IFormFileCollection uploads)
        {
            // Проверяем, чтобы размер файлов не превышал заданный объем
            foreach (var file in uploads)
            {
                if (file.Length > 2097152)
                {
                    ModelState.AddModelError("GalleryImage", $"Файл \"{file.FileName}\" превышает установленный лимит 2MB.");
                    break;
                }
            }

            // Если все в порядке, заходим в тело условия
            if (ModelState.IsValid)
            {
                // Создаем экземпляр класса News и присваиваем ему значения
                Gallery gallery = new Gallery()
                {
                    GalleryTitle = model.GalleryTitle,
                    GalleryDescription = model.GalleryDescription,
                    // Скрытые поля
                    Id = model.GalleryId,
                    GalleryDate = model.GalleryDate,
                    UserName = model.UserName
                };

                // Далее начинаем обработку загружаемых изображений
                List<GalleryImage> galleryImages = new List<GalleryImage>();
                foreach (var uploadedImage in uploads)
                {
                    // Если размер входного файла больше 0, заходим в тело условия
                    if (uploadedImage.Length > 0)
                    {
                        // Создаем новый объект класса FileInfo из полученного изображения для дальнейшей обработки
                        FileInfo imgFile = new FileInfo(uploadedImage.FileName);
                        // Приводим расширение к нижнему регистру (если оно было в верхнем)
                        string imgExtension = imgFile.Extension.ToLower();
                        // Генерируем новое имя для файла
                        string newFileName = Guid.NewGuid() + imgExtension;
                        // Пути сохранения файла
                        string pathNormal = "/files/images/normal/" + newFileName; // изображение исходного размера
                        string pathScaled = "/files/images/scaled/" + newFileName; // уменьшенное изображение

                        // В операторе try/catch делаем уменьшенную копию изображения.
                        // Если входным файлом окажется не изображение, нас перекинет в блок CATCH и выведет сообщение об ошибке
                        try
                        {
                            // Создаем объект класса SixLabors.ImageSharp.Image и грузим в него полученное изображение
                            using (Image image = Image.Load(uploadedImage.OpenReadStream()))
                            {
                                // Создаем уменьшенную копию и обрезаем её
                                var clone = image.Clone(x => x.Resize(new ResizeOptions
                                {
                                    Mode = ResizeMode.Crop,
                                    Size = new Size(300, 200)
                                }));
                                // Сохраняем уменьшенную копию
                                await clone.SaveAsync(_appEnvironment.WebRootPath + pathScaled, new JpegEncoder { Quality = 50 });
                                // Сохраняем исходное изображение
                                await image.SaveAsync(_appEnvironment.WebRootPath + pathNormal);
                            }
                        }
                        // Если вдруг что-то пошло не так (например, на вход подало не картинку), то выводим сообщение об ошибке
                        catch
                        {
                            // Создаем сообщение об ошибке для вывода пользователю
                            ModelState.AddModelError("GalleryImage", $"Файл {uploadedImage.FileName} имеет неверный формат.");

                            // Удаляем только что созданные файлы (если ошибка возникла не на первом файле)
                            foreach (var image in galleryImages)
                            {
                                // Исходные (полноразмерные) изображения
                                FileInfo imageNormal = new FileInfo(_appEnvironment.WebRootPath + image.ImagePathNormal);
                                if (imageNormal.Exists)
                                {
                                    imageNormal.Delete();
                                }
                                // И их уменьшенные копии
                                FileInfo imageScaled = new FileInfo(_appEnvironment.WebRootPath + image.ImagePathScaled);
                                if (imageScaled.Exists)
                                {
                                    imageScaled.Delete();
                                }
                            }
                            // Возвращаем модель с сообщением об ошибке в представление
                            return View(model);
                        }

                        // Создаем объект класса NewsImage со всеми параметрами
                        GalleryImage galleryImage = new GalleryImage()
                        {
                            Id = Guid.NewGuid(),
                            ImageName = newFileName,
                            ImagePathNormal = pathNormal,
                            ImagePathScaled = pathScaled,
                            GalleryId = gallery.Id
                        };
                        // Добавляем объект newsImage в список newsImages
                        galleryImages.Add(galleryImage);
                    }
                }

                // Если в процессе выполнения не возникло ошибок, сохраняем всё в БД
                if (galleryImages != null && galleryImages.Count > 0)
                {
                    await cmsDB.GalleryImages.AddRangeAsync(galleryImages);
                }
                cmsDB.Galleries.Update(gallery);
                await cmsDB.SaveChangesAsync();

                // Редирект на главную страницу
                //return RedirectToAction("Index", "News");

                List<GalleryImage> images2 = new List<GalleryImage>();
                foreach (var image in cmsDB.GalleryImages)
                {
                    if (image.GalleryId == model.GalleryId)
                    {
                        images2.Add(image);
                    }
                }
                model.GalleryImages = images2;
                model.ImagesCount = images2.Count;
                return View(model);
            }

            // В случае, если при редактировании пытаться загрузить картинку выше разрешенного лимита, то перестают отображаться уже имеющиеся изображения
            // При перегонке модели из гет в пост, теряется список с изображениями. Причина пока не ясна, поэтому сделал такой костыль
            // Счетчик соответственно тоже обнулялся, поэтому его тоже приходится переназначать заново
            List<GalleryImage> images = new List<GalleryImage>();
            foreach (var image in cmsDB.GalleryImages)
            {
                if (image.GalleryId == model.GalleryId)
                {
                    images.Add(image);
                }
            }
            model.GalleryImages = images;
            model.ImagesCount = images.Count;

            // Возврат модели в представление в случае, если запорится валидация
            return View(model);
        }
        #endregion

        #region Добавить изображение [GET]
        public IActionResult AddImage(Guid galleryId)
        {
            ViewBag.GalleryId = galleryId;

            return View();
        }
        #endregion

        #region Добавить изображение [POST]
        [HttpPost]
        public async Task<IActionResult> AddImage(AddImageViewModel model, IFormFileCollection uploads)
        {
            // Проверяем, чтобы размер файлов не превышал заданный объем
            foreach (var file in uploads)
            {
                if (file.Length > 2097152)
                {
                    ModelState.AddModelError("GalleryImage", $"Файл \"{file.FileName}\" превышает установленный лимит 2MB.");
                    break;
                }
            }

            // Если все в порядке, заходим в тело условия
            if (ModelState.IsValid)
            {
                // Далее начинаем обработку изображений
                List<GalleryImage> galleryImages = new List<GalleryImage>();
                foreach (var uploadedImage in uploads)
                {
                    // Если размер входного файла больше 0, заходим в тело условия
                    if (uploadedImage.Length > 0)
                    {
                        // Создаем новый объект класса FileInfo из полученного изображения для дальнейшей обработки
                        FileInfo imgFile = new FileInfo(uploadedImage.FileName);
                        // Приводим расширение к нижнему регистру (если оно было в верхнем)
                        string imgExtension = imgFile.Extension.ToLower();
                        // Генерируем новое имя для файла
                        string newFileName = Guid.NewGuid() + imgExtension;
                        // Пути сохранения файла
                        string pathNormal = "/files/images/normal/" + newFileName; // изображение исходного размера
                        string pathScaled = "/files/images/scaled/" + newFileName; // уменьшенное изображение

                        // В операторе try/catch делаем уменьшенную копию изображения.
                        // Если входным файлом окажется не изображение, нас перекинет в блок CATCH и выведет сообщение об ошибке
                        try
                        {
                            // Создаем объект класса SixLabors.ImageSharp.Image и грузим в него полученное изображение
                            using (Image image = Image.Load(uploadedImage.OpenReadStream()))
                            {
                                // Создаем уменьшенную копию и обрезаем её
                                var clone = image.Clone(x => x.Resize(new ResizeOptions
                                {
                                    Mode = ResizeMode.Crop,
                                    Size = new Size(300, 200)
                                }));
                                // Сохраняем уменьшенную копию
                                await clone.SaveAsync(_appEnvironment.WebRootPath + pathScaled, new JpegEncoder { Quality = 50 });
                                // Сохраняем исходное изображение
                                await image.SaveAsync(_appEnvironment.WebRootPath + pathNormal);
                            }

                        }
                        // Если вдруг что-то пошло не так (например, на вход подало не картинку), то выводим сообщение об ошибке
                        catch
                        {
                            // Создаем сообщение об ошибке для вывода пользователю
                            ModelState.AddModelError("GalleryImage", $"Файл {uploadedImage.FileName} имеет неверный формат.");

                            // Удаляем только что созданные файлы (если ошибка возникла не на первом файле)
                            foreach (var image in galleryImages)
                            {
                                // Исходные (полноразмерные) изображения
                                FileInfo imageNormal = new FileInfo(_appEnvironment.WebRootPath + image.ImagePathNormal);
                                if (imageNormal.Exists)
                                {
                                    imageNormal.Delete();
                                }
                                // И их уменьшенные копии
                                FileInfo imageScaled = new FileInfo(_appEnvironment.WebRootPath + image.ImagePathScaled);
                                if (imageScaled.Exists)
                                {
                                    imageScaled.Delete();
                                }
                            }

                            // Возвращаем модель с сообщением об ошибке в представление
                            return View(model);
                        }

                        // Создаем объект класса NewsImage со всеми параметрами
                        GalleryImage galleryImage = new GalleryImage()
                        {
                            Id = Guid.NewGuid(),
                            ImageName = newFileName,
                            ImagePathNormal = pathNormal,
                            ImagePathScaled = pathScaled,
                            ImageDate = DateTime.Now,
                            GalleryId = model.GalleryId
                        };
                        // Добавляем объект newsImage в список newsImages
                        galleryImages.Add(galleryImage);
                    }
                }

                // Если в процессе выполнения не возникло ошибок, сохраняем всё в БД
                if (galleryImages != null && galleryImages.Count > 0)
                {
                    await cmsDB.GalleryImages.AddRangeAsync(galleryImages);
                    await cmsDB.SaveChangesAsync();
                }

                // Редирект на главную страницу
                //return RedirectToAction("ViewGallery", "Gallery", model);
            }
            // Возврат модели в представление в случае, если запорится валидация
            //return RedirectToAction("ViewGallery", "Gallery", new AddImageViewModel() { GalleryId = galleryId });
            return View(model);
        }
        #endregion


    }
}
