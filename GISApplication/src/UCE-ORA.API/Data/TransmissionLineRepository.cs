using Microsoft.EntityFrameworkCore;
using UCE_ORA.API.Models;

namespace UCE_ORA.API.Data;

public class TransmissionLineRepository
{
    private readonly AppDbContext _context;

    public TransmissionLineRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<TransmissionLine>> GetAllAsync()
    {
        return await _context.TransmissionLines.ToListAsync();
    }
}
