using Microsoft.AspNetCore.Mvc;
using GestorComentariosAPI.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GestorComentariosAPI.Controllers
{
    [ApiController]
    [Route("api/comentarios")]
    public class ComentariosController : ControllerBase
    {
        private readonly HttpClient _httpClient;

        public ComentariosController()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://jsonplaceholder.typicode.com/")
            };
        }


        [HttpGet("post/{postId}")]
        public async Task<IActionResult> GetComentariosPorPost(int postId)
        {
            if (postId < 1 || postId > 100)
                return BadRequest("El postId debe estar entre 1 y 100.");

            var response = await _httpClient.GetAsync($"comments?postId={postId}");
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode);

            var json = await response.Content.ReadAsStringAsync();
            var comentarios = JsonSerializer.Deserialize<List<Comentario>>(json) ?? new List<Comentario>();

            if (!comentarios.Any())
                return NotFound("Este post no tiene comentarios. Puede agregar uno.");

            var resultado = comentarios.Select(c => new
            {
                id = c.Id,
                autor = c.Name,
                email = c.Email,
                emailValido = !string.IsNullOrEmpty(c.Email) &&
                              Regex.IsMatch(c.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"),
                contenido = c.Body,
                contenidoResumido = !string.IsNullOrEmpty(c.Body) && c.Body.Length > 50
                    ? c.Body.Substring(0, 50) + "..."
                    : c.Body
            });

            return Ok(new
            {
                postId,
                totalComentarios = comentarios.Count,
                comentarios = resultado
            });
        }


        [HttpGet("email")]
        public async Task<IActionResult> BuscarPorDominio([FromQuery] string dominio)
        {
            if (string.IsNullOrWhiteSpace(dominio))
                return BadRequest("Debe especificar un dominio.");

            var response = await _httpClient.GetAsync("comments");
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode);

            var json = await response.Content.ReadAsStringAsync();
            var comentarios = JsonSerializer.Deserialize<List<Comentario>>(json) ?? new List<Comentario>();

            var filtrados = comentarios
                .Where(c => !string.IsNullOrEmpty(c.Email) &&
                            c.Email!.ToLower().Contains(dominio.ToLower()))
                .ToList();

            var distribucion = filtrados
                .Where(c => !string.IsNullOrEmpty(c.Email))
                .GroupBy(c => c.Email!.Substring(c.Email.IndexOf("@")))
                .ToDictionary(g => g.Key, g => g.Count());

            var resultado = filtrados.Select(c => new
            {
                id = c.Id,
                autor = c.Name,
                email = c.Email,
                contenidoResumido = !string.IsNullOrEmpty(c.Body) && c.Body.Length > 30
                    ? c.Body.Substring(0, 30) + "..."
                    : c.Body
            });

            return Ok(new
            {
                dominioBuscado = dominio,
                totalEncontrados = filtrados.Count,
                distribucionPorDominio = distribucion,
                comentarios = resultado
            });
        }


        [HttpGet("post/{postId}/analisis")]
        public async Task<IActionResult> Analisis(int postId)
        {
            if (postId < 1 || postId > 100)
                return BadRequest("El postId debe estar entre 1 y 100.");

            var response = await _httpClient.GetAsync($"comments?postId={postId}");
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode);

            var json = await response.Content.ReadAsStringAsync();
            var comentarios = JsonSerializer.Deserialize<List<Comentario>>(json) ?? new List<Comentario>();

            if (!comentarios.Any())
                return NotFound("Este post no tiene comentarios.");

            Regex regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");

            int total = comentarios.Count;

            int emailsValidos = comentarios.Count(c =>
                !string.IsNullOrEmpty(c.Email) && regex.IsMatch(c.Email!));

            var topDominios = comentarios
                .Where(c => !string.IsNullOrEmpty(c.Email))
                .GroupBy(c => c.Email!.Substring(c.Email.IndexOf("@")))
                .OrderByDescending(g => g.Count())
                .Take(5)
                .ToDictionary(g => g.Key, g => g.Count());

            var topAutores = comentarios
                .Where(c => !string.IsNullOrEmpty(c.Name))
                .GroupBy(c => c.Name!)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .ToDictionary(g => g.Key, g => g.Count());

            var palabras = comentarios
                .Where(c => !string.IsNullOrEmpty(c.Body))
                .SelectMany(c => c.Body!
                    .ToLower()
                    .Split(new char[] { ' ', '.', ',', '\n', '\r' },
                        StringSplitOptions.RemoveEmptyEntries))
                .Where(p => p.Length > 3);

            var palabraMasComun = palabras
                .GroupBy(p => p)
                .OrderByDescending(g => g.Count())
                .Select(g => new
                {
                    palabra = g.Key,
                    frecuencia = g.Count()
                })
                .FirstOrDefault();

            double promedioLongitud = comentarios
                .Where(c => !string.IsNullOrEmpty(c.Body))
                .Average(c => c.Body!.Length);

            return Ok(new
            {
                postId,
                analisis = new
                {
                    totalComentarios = total,
                    emailsValidos,
                    porcentajeValido = total > 0
                        ? $"{((double)emailsValidos / total * 100):F1}%"
                        : "0%",
                    topDominios,
                    topAutores,
                    palabraMasComun,
                    longitudPromedio = promedioLongitud
                }
            });
        }


        [HttpGet("aleatorio")]
        public async Task<IActionResult> ComentarioAleatorio()
        {
            Random random = new Random();
            Comentario? comentario = null;
            int intentos = 0;

            while (comentario == null && intentos < 3)
            {
                int id = random.Next(1, 501);
                var response = await _httpClient.GetAsync($"comments/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    comentario = JsonSerializer.Deserialize<Comentario>(json);
                }

                intentos++;
            }

            if (comentario == null)
                return NotFound("No se pudo obtener comentario válido.");

            return Ok(new
            {
                comentarioAleatorio = new
                {
                    id = comentario.Id,
                    autor = comentario.Name,
                    email = comentario.Email,
                    mensaje = !string.IsNullOrEmpty(comentario.Body) &&
                              comentario.Body.Length > 100
                        ? comentario.Body.Substring(0, 100) + "..."
                        : comentario.Body
                },
                sugerencias = new
                {
                    verConversacionCompleta = $"/api/comentarios/post/{comentario.PostId}",
                    otrosComentariosDelAutor = $"/api/comentarios/email?dominio={comentario.Name}"
                }
            });
        }
    }
}