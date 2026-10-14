namespace TallerChat.Api.Tests.Unit;

/// <summary>Fragmentos de conocimiento (lo que el chat recupera de MongoDB) y su plantilla de correo.</summary>
public class ChatContentTests
{
    [Fact]
    public void Sections_solo_devuelve_los_campos_rellenos_y_en_orden_de_lectura()
    {
        var chunk = new KnowledgeChunk
        {
            ChunkId = "frenos-001", Title = "Chirrido al frenar", Category = "Frenos", SubCategory = "Pastillas",
            ProcedimientoResolucion = "Sustituir pastillas.",
            Sintomatologia = "Chirrido metálico.",
            CausasProbables = ["Pastillas gastadas", "Discos rayados"],
            BuenasPracticas = "   ", // en blanco: no cuenta
            Especificaciones = new Especificaciones { CorreaDistribucion = "Cada 120.000 km" }
        };

        var sections = chunk.Sections().ToList();
        Assert.Equal(["Sintomatología", "Causas probables", "Procedimiento de resolución", "Correa de distribución"],
            sections.Select(s => s.Label));
        Assert.Equal("Pastillas gastadas; Discos rayados", sections[1].Text);
    }

    [Fact]
    public void ToSource_numera_desde_la_referencia_dada()
    {
        var chunk = new KnowledgeChunk { ChunkId = "x-1", Title = "T", Category = "C", SubCategory = "S", Score = 1.5, Sintomatologia = "s" };
        var src = chunk.ToSource(3);
        Assert.Equal(3, src.Ref);
        Assert.Equal("x-1", src.ChunkId);
        Assert.Equal(1.5, src.Score);
        Assert.Single(src.Sections);
    }

    [Fact]
    public void La_plantilla_del_chat_codifica_el_HTML_y_resalta_las_citas()
    {
        var items = new List<EmailItem>
        {
            new("¿Qué es <b>esto</b>?", "Respuesta con <img src=x onerror=alert(1)> y cita [1][2].\nSegunda linea.",
                [new SourceRef(1, "c-1", "Título <i>raro</i>", "Cat", "Sub", [], 1, [new Section("Etiqueta", "Texto & mas")])])
        };

        var (html, text) = EmailTemplate.Render(items);

        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("<b>esto</b>", html);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html);
        Assert.Contains("Segunda linea", html);
        Assert.Contains("<br>", html);
        Assert.Contains(">1</span>", html); // cita [1] convertida en chip
        Assert.Contains("Texto &amp; mas", html);
        Assert.Contains("PREGUNTA: ¿Qué es <b>esto</b>?", text); // el texto plano va sin codificar
        Assert.Contains("[1] Título <i>raro</i>", text);
    }
}
