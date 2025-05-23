using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Biblioteca.Data;
using Biblioteca.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;


namespace Biblioteca.Controllers
{
    public class AvaliacoesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AvaliacoesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Avaliacoes
        public async Task<IActionResult> Index()
        {
            var avaliacoes = _context.Avaliacoes
                .Include(a => a.Livro)
                .Include(a => a.Usuario)
                    .ThenInclude(u => u.IdentityUser); // Inclui o IdentityUser do Usuario

            return View(await avaliacoes.ToListAsync());
        }



        // GET: Avaliacoes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var avaliacao = await _context.Avaliacoes
                .Include(a => a.Livro)
                .Include(a => a.Usuario)
                .FirstOrDefaultAsync(m => m.AvaliacaoId == id);
            if (avaliacao == null)
            {
                return NotFound();
            }

            return View(avaliacao);
        }

        // GET: Avaliacoes/Create
        public IActionResult Create(int? livroId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var usuario = _context.Usuarios.FirstOrDefault(u => u.AppUserId.ToString() == userId);

            if (usuario == null)
                return Unauthorized();

            // Busca os IDs dos livros já retirados pelo usuário (e não devolvidos/cancelados)
            var livrosRetiradosIds = _context.Movimentacoes
                .Where(m => m.UsuarioId == usuario.UsuarioId && m.LivroDevolvido)
                .Select(m => m.LivroId)
                .Distinct()
                .ToList();

            // Busca os livros correspondentes
            var livrosRetirados = _context.Livros
                .Where(l => livrosRetiradosIds.Contains(l.LivroId))
                .ToList();

            var avaliacao = new Avaliacao
            {
                UsuarioId = usuario.UsuarioId
            };

            if (livroId.HasValue && livrosRetiradosIds.Contains(livroId.Value))
                avaliacao.LivroId = livroId.Value;

            ViewData["LivroId"] = new SelectList(livrosRetirados, "LivroId", "Titulo", avaliacao.LivroId);
            ViewBag.NomeCompleto = usuario.NomeCompleto;

            return View(avaliacao);
        }


        // POST: Avaliacoes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AvaliacaoId,Nota,Comentario,LivroId")] Avaliacao avaliacao)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var usuario = _context.Usuarios.FirstOrDefault(u => u.AppUserId.ToString() == userId);

            if (usuario == null)
                return Unauthorized();

            // Busca os IDs dos livros já retirados pelo usuário (e devolvidos)
            var livrosRetiradosIds = _context.Movimentacoes
                .Where(m => m.UsuarioId == usuario.UsuarioId && m.LivroDevolvido)
                .Select(m => m.LivroId)
                .Distinct()
                .ToList();

            // RESTRIÇÃO: só permite avaliar livros que o usuário retirou
            if (!livrosRetiradosIds.Contains(avaliacao.LivroId))
            {
                ModelState.AddModelError(nameof(avaliacao.LivroId), "Você só pode avaliar livros que já retirou.");
            }

            // Garante que a avaliação será sempre do usuário logado e com a data atual
            avaliacao.UsuarioId = usuario.UsuarioId;
            avaliacao.DataAvaliacao = DateTime.Now;

            if (ModelState.IsValid)
            {
                _context.Add(avaliacao);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // Repita a lógica do GET para popular o select corretamente
            var livrosRetirados = _context.Livros
                .Where(l => livrosRetiradosIds.Contains(l.LivroId))
                .ToList();

            ViewData["LivroId"] = new SelectList(livrosRetirados, "LivroId", "Titulo", avaliacao.LivroId);
            ViewBag.NomeCompleto = usuario.NomeCompleto;

            return View(avaliacao);
        }

        // GET: Avaliacoes/Edit/5
        [Authorize(Roles = "Aluno")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var avaliacao = await _context.Avaliacoes
                .Include(a => a.Usuario)
                .FirstOrDefaultAsync(a => a.AvaliacaoId == id);

            if (avaliacao == null)
                return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Só permite se o usuário logado for o dono E NÃO for admin
            if (avaliacao.Usuario?.AppUserId?.ToString() != userId || User.IsInRole("Admin"))
                return Forbid();

            ViewData["LivroId"] = new SelectList(_context.Livros, "LivroId", "Titulo", avaliacao.LivroId);
            return View(avaliacao);
        }

        // POST: Avaliacoes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Aluno")]
        public async Task<IActionResult> Edit(int id, [Bind("AvaliacaoId,Nota,Comentario,DataAvaliacao,LivroId")] Avaliacao avaliacao)
        {
            if (id != avaliacao.AvaliacaoId)
                return NotFound();

            // Recupera a avaliação original do banco
            var original = await _context.Avaliacoes.AsNoTracking()
                .Include(a => a.Usuario)
                .FirstOrDefaultAsync(a => a.AvaliacaoId == id);

            if (original == null)
                return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Só permite se o usuário logado for o dono E NÃO for admin
            if (original.Usuario?.AppUserId?.ToString() != userId || User.IsInRole("Admin"))
                return Forbid();

            if (ModelState.IsValid)
            {
                try
                {
                    avaliacao.UsuarioId = original.UsuarioId; // Garante que o usuário não muda
                    avaliacao.DataAvaliacao = original.DataAvaliacao; // Garante que a data não muda

                    _context.Update(avaliacao);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Avaliacoes.Any(e => e.AvaliacaoId == avaliacao.AvaliacaoId))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["LivroId"] = new SelectList(_context.Livros, "LivroId", "Titulo", avaliacao.LivroId);
            return View(avaliacao);
        }


        // GET: Avaliacoes/Delete/5
        [Authorize(Roles = "Aluno")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var avaliacao = await _context.Avaliacoes
                .Include(a => a.Livro)
                .Include(a => a.Usuario)
                .FirstOrDefaultAsync(m => m.AvaliacaoId == id);

            if (avaliacao == null)
                return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Só permite se o usuário logado for o dono E NÃO for admin
            if (avaliacao.Usuario?.AppUserId?.ToString() != userId || User.IsInRole("Admin"))
                return Forbid();

            return View(avaliacao);
        }

        // POST: Avaliacoes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Aluno")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var avaliacao = await _context.Avaliacoes
                .Include(a => a.Usuario)
                .FirstOrDefaultAsync(a => a.AvaliacaoId == id);

            if (avaliacao == null)
                return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Só permite se o usuário logado for o dono E NÃO for admin
            if (avaliacao.Usuario?.AppUserId?.ToString() != userId || User.IsInRole("Admin"))
                return Forbid();

            _context.Avaliacoes.Remove(avaliacao);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
