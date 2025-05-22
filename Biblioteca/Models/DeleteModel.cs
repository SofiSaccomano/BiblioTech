public class DeleteModel : PageModel
{
    private readonly BibliotecaContext _context;

    public DeleteModel(BibliotecaContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Livro Livro { get; set; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null)
            return NotFound();

        Livro = await _context.Livros
            .Include(l => l.Genero)
            .FirstOrDefaultAsync(m => m.LivroId == id);

        if (Livro == null)
            return NotFound();

        // Livro.UrlCapa estará preenchido se o banco estiver correto
        return Page();
    }
}
