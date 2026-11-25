/// Algo que hay que cumplir para dar una sala por COMPLETADA (oleadas de enemigos, un puzzle...).
/// Cuando TODOS los objetivos de una sala estan hechos, la sala queda completada.
public interface IRoomObjective
{
    bool IsDone { get; }

    /// El jugador entro (o reaparecio) en la sala: el objetivo empieza. Debe poder llamarse varias veces sin problema.
    void Begin();
}
