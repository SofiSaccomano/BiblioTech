using Biblioteca.Data;
using Biblioteca.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;


namespace Biblioteca.Controllers
{
    [Authorize]
    public class ReservasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReservasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Reservas
        [Authorize]
        public async Task<IActionResult> Index(string searchTerm = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Index", "Home");

            var reservasQuery = _context.Reservas
                .Include(r => r.Livro)
                .Include(r => r.Usuario)
                .Where(r => r.Usuario.AppUserId.ToString() == userId && !r.Cancelada); // <-- Adicione este filtro

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                reservasQuery = reservasQuery.Where(r =>
                    r.Livro.Titulo.Contains(searchTerm) ||
                    r.Usuario.NomeCompleto.Contains(searchTerm));
            }

            var reservas = await reservasQuery
                .OrderBy(r => r.LivroRetirado)
                .ThenBy(r => r.DataReserva)
                .ToListAsync();

            ViewBag.SearchTerm = searchTerm;
            return View(reservas);
        }


        // GET: Reservas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reserva = await _context.Reservas
                .Include(r => r.Livro)
                .Include(r => r.Usuario)
                .FirstOrDefaultAsync(m => m.ReservaId == id);
            if (reserva == null)
            {
                return NotFound();
            }

            return View(reserva);
        }

        // GET: Reservas/Create
        public IActionResult Create()
        {
            ViewData["LivroId"] = new SelectList(_context.Livros, "LivroId", "Titulo");
            ViewData["UsuarioId"] = new SelectList(_context.Usuarios, "UsuarioId", "NomeCompleto");
            return View();
        }

        // POST: Reservas/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
     int LivroId,
     string? searchTerm,
     string? sortOrder,
     int page = 1)
        {
            // Obtém o usuário logado (Identity)
            var userName = User.Identity?.Name;

            // Busca o usuário no banco de dados
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.IdentityUser != null && u.IdentityUser.UserName == userName);

            if (usuario == null)
            {
                return Unauthorized();
            }

            // Verifica se o livro tem quantidade disponivel para reserva
            var livro = await _context.Livros.FindAsync(LivroId);

            if (livro == null || livro.Quantidade <= 0)
            {
                // Livro não encontrado ou sem quantidade disponível
                var livrosQuery = _context.Livros.Include(l => l.Genero).AsQueryable();

                // Busca
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    livrosQuery = livrosQuery.Where(l =>
                        l.Titulo.Contains(searchTerm) ||
                        l.Autor.Contains(searchTerm)
                    );
                }

                // Ordenação
                switch (sortOrder)
                {
                    case "titulo_desc":
                        livrosQuery = livrosQuery.OrderByDescending(l => l.Titulo);
                        break;
                    case "titulo_asc":
                        livrosQuery = livrosQuery.OrderBy(l => l.Titulo);
                        break;
                    case "autor_desc":
                        livrosQuery = livrosQuery.OrderByDescending(l => l.Autor);
                        break;
                    case "autor_asc":
                        livrosQuery = livrosQuery.OrderBy(l => l.Autor);
                        break;
                    case "editora_desc":
                        livrosQuery = livrosQuery.OrderByDescending(l => l.Editora);
                        break;
                    case "editora_asc":
                        livrosQuery = livrosQuery.OrderBy(l => l.Editora);
                        break;
                    case "genero_desc":
                        livrosQuery = livrosQuery.OrderByDescending(l => l.Genero.Nome);
                        break;
                    case "genero_asc":
                        livrosQuery = livrosQuery.OrderBy(l => l.Genero.Nome);
                        break;
                    default:
                        livrosQuery = livrosQuery.OrderBy(l => l.Titulo);
                        break;
                }

                // Paginação
                int pageSize = 5;
                int totalCount = await livrosQuery.CountAsync();
                var livros = await livrosQuery
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var viewModel = new LivroListViewModel
                {
                    Livros = livros,
                    PageIndex = page,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    SearchTerm = searchTerm,
                    SortOrder = sortOrder,
                    ErrorMessage = "Livro não disponível para reserva."
                };

                // Retorna a view de livros com paginação, ordenação e busca
                return View("~/Views/Livros/Index.cshtml", viewModel);
            }

            // Atualiza a quantidade do livro
            livro.Quantidade -= 1;
            _context.Update(livro);

            // Cria a reserva
            var reserva = new Reserva
            {
                LivroId = LivroId,
                UsuarioId = usuario.UsuarioId,
                DataReserva = DateTime.Now,
                LivroRetirado = false,
                Cancelada = false
            };

            _context.Add(reserva);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: Reservas/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reserva = await _context.Reservas.FindAsync(id);
            if (reserva == null)
            {
                return NotFound();
            }
            ViewData["LivroId"] = new SelectList(_context.Livros, "LivroId", "LivroId", reserva.LivroId);
            ViewData["UsuarioId"] = new SelectList(_context.Usuarios, "UsuarioId", "UsuarioId", reserva.UsuarioId);
            return View(reserva);
        }

        // POST: Reservas/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ReservaId,DataReserva,UsuarioId,LivroId,LivroRetirado,Cancelada")] Reserva reserva)
        {
            if (id != reserva.ReservaId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(reserva);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ReservaExists(reserva.ReservaId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["LivroId"] = new SelectList(_context.Livros, "LivroId", "LivroId", reserva.LivroId);
            ViewData["UsuarioId"] = new SelectList(_context.Usuarios, "UsuarioId", "UsuarioId", reserva.UsuarioId);
            return View(reserva);
        }

        // GET: Reservas/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reserva = await _context.Reservas
                .Include(r => r.Livro)
                .Include(r => r.Usuario)
                .FirstOrDefaultAsync(m => m.ReservaId == id);
            if (reserva == null)
            {
                return NotFound();
            }

            return View(reserva);
        }

        // POST: Reservas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var reserva = await _context.Reservas.FindAsync(id);
            if (reserva != null)
            {
                _context.Reservas.Remove(reserva);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ReservaExists(int id)
        {
            return _context.Reservas.Any(e => e.ReservaId == id);
        }

        [AllowAnonymous]
        public async Task<List<Livro>> Top5MaisReservados()
        {
            var top5 = await _context.Reservas
                .GroupBy(r => r.LivroId)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key)
                .ToListAsync();

            var livros = await _context.Livros
                .Include(l => l.Genero)
                .Where(l => top5.Contains(l.LivroId))
                .ToListAsync();

            // Ordena os livros conforme a ordem do top5
            livros = top5.Select(id => livros.First(l => l.LivroId == id)).ToList();

            return livros;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int id)
        {
            // Busca a reserva pelo id
            var reserva = await _context.Reservas
                .FirstOrDefaultAsync(r => r.ReservaId == id);

            if (reserva == null)
            {
                TempData["SuccessMessage"] = "Reserva não encontrada.";
                return RedirectToAction("Index");
            }

            // Remove a movimentação associada, se existir
            var movimentacao = await _context.Movimentacoes
                .FirstOrDefaultAsync(m => m.LivroId == reserva.LivroId && m.UsuarioId == reserva.UsuarioId && !m.LivroDevolvido);

            if (movimentacao != null)
            {
                _context.Movimentacoes.Remove(movimentacao);
            }

            // Remove a reserva
            _context.Reservas.Remove(reserva);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Reserva cancelada e retirada removida com sucesso!";
            return RedirectToAction("Index");
        }



    }
}