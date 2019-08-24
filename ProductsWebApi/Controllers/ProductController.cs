﻿using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductsWebApi.Models;
using ProductsWebApi.Models.Entities;
using ProductsWebApi.Models.Json;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ProductsWebApi.Controllers
{
    [Produces("application/json")]
    [Route("api/product")]
    public class ProductController : Controller
    {
        private readonly IHostingEnvironment _env;
        private readonly IRepository<ProductEntity> _productRepository;
        private readonly IMapper _mapper;

        public ProductController(IHostingEnvironment env, IRepository<ProductEntity> productRepository, IMapper mapper)
        {
            _env = env;
            _productRepository = productRepository;
            _mapper = mapper;

        }

        // GET: api/product
        [HttpGet]
        public IEnumerable<ProductBase> GetProductEntity(string name, double? priceMin, double? priceMax)
        {
            var productEntities = _productRepository.GetItemList();

            if (name != null)
            {
                productEntities = productEntities.Where(product => product.Name.StartsWith(name));
            }

            if (priceMin.HasValue)
            {
                productEntities = productEntities.Where(product => product.Price >= priceMin.Value);
            }

            if (priceMax.HasValue)
            {
                productEntities = productEntities.Where(product => product.Price <= priceMax.Value);
            }

            List<ProductBase> products = new List<ProductBase>();
            foreach (var product in productEntities)
            {
                products.Add(new ProductBase { Id = product.Id, Name = product.Name, Price = product.Price });
            }

            return products;
        }

        // GET: api/product/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProductEntity([FromRoute] long id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var productEntity = await _productRepository.GetItemById(id);

            if (productEntity == null)
            {
                return NotFound();
            }

            try
            {
                if (productEntity.PictureName != null)
                {
                    productEntity.PictureName =
                        Path.Combine(_env.ContentRootPath, "Images", productEntity.PictureName);
                }
                
                return Ok(productEntity);
            }
            catch (System.Exception e)
            {
                Debug.WriteLine(e);

                return BadRequest();
            }
        }

        // PUT: api/product/5
        [HttpPut("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> PutProductEntity([FromRoute] long id, [FromBody] Product product)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var productEntity = _mapper.Map<ProductEntity>(product);
                productEntity.Id = id;
                productEntity.PictureName = await SaveImage(product);

                _productRepository.Update(productEntity);

                await _productRepository.Save();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductEntityExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/product
        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> PostProductEntity([FromBody] Product product)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var productEntity = _mapper.Map<ProductEntity>(product);
            productEntity.PictureName = await SaveImage(product);

            await _productRepository.Create(productEntity);

            return CreatedAtAction("PostProductEntity", new { id = productEntity.Id }, product);
        }

        // DELETE: api/product/5
        [Authorize(Roles = "admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProductEntity([FromRoute] long id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var productEntity = await _productRepository.GetItemById(id);
            if (productEntity == null)
            {
                return NotFound();
            }

            if (productEntity.PictureName != null)
            {
                try
                {
                    string filePath = Path.Combine(_env.ContentRootPath, "Images", productEntity.PictureName);
                    System.IO.File.Delete(filePath);
                }
                catch(System.Exception e)
                {
                    Debug.WriteLine(e);
                }
            }
            
            await _productRepository.Remove(productEntity);

            return Ok(productEntity);
        }

        private bool ProductEntityExists(long id)
        {
            return _productRepository.GetItemList().Any(e => e.Id == id);
        }

        private async Task<string> SaveImage(Product product)
        {
            string fileName = product.PictureName;

            if (product.Image != null && product.Image.Length > 0)
            {
                fileName = product.Image.FileName;
                string filePath = Path.Combine(_env.ContentRootPath, "Images", fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await product.Image.CopyToAsync(stream);
                }
            }

            return fileName;
        }
    }
}