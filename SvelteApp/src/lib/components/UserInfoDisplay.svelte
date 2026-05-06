<script>
  export let customer = null;
  export let licenseNumber = '';
  let showBookings = false;

  $: fullName = customer ? `${customer.firstName || ''} ${customer.lastName || ''}`.trim() : '';
  $: location = [customer?.city, customer?.country].filter(Boolean).join(', ');
  $: initials = fullName
    ? fullName
        .split(/\s+/)
        .slice(0, 2)
        .map((part) => part[0])
        .join('')
        .toUpperCase()
    : 'U';

  function formatDate(value) {
    if (!value) {
      return 'Pendiente';
    }

    return new Intl.DateTimeFormat('es-PR', {
      dateStyle: 'medium',
      timeStyle: 'short'
    }).format(new Date(value));
  }
</script>

<aside class="mb-6 rounded-md border border-slate-200 bg-slate-50 px-4 py-4">
  {#if customer}
    <div class="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
      <div class="flex gap-4">
        <div class="flex h-12 w-12 shrink-0 items-center justify-center rounded-md bg-slate-950 text-sm font-bold text-white">
          {initials}
        </div>
        <div>
          <p class="text-xs font-semibold uppercase text-slate-500">Cuenta encontrada</p>
          <h2 class="mt-1 text-lg font-bold tracking-normal text-slate-950">{fullName}</h2>
          <p class="mt-1 text-sm text-slate-600">{customer.customerCode}</p>
        </div>
      </div>

      <button
        type="button"
        class="w-fit rounded-full bg-white px-3 py-1 text-xs font-semibold text-slate-700 ring-1 ring-slate-200 transition hover:bg-slate-100"
        on:click={() => (showBookings = true)}>
        Ver Reservas ({customer.bookings?.length || 0})
      </button>
    </div>

    <dl class="mt-5 grid gap-4 border-t border-slate-200 pt-4 text-sm md:grid-cols-2 lg:grid-cols-3">
      <div>
        <dt class="font-semibold text-slate-800">Licencia</dt>
        <dd class="mt-1 text-slate-600">{customer.licenseNumber || 'No registrada'}</dd>
      </div>
      <div>
        <dt class="font-semibold text-slate-800">Telefono</dt>
        <dd class="mt-1 text-slate-600">{customer.phoneNumber || 'No registrado'}</dd>
      </div>
      <div>
        <dt class="font-semibold text-slate-800">Email</dt>
        <dd class="mt-1 break-words text-slate-600">{customer.email || 'No registrado'}</dd>
      </div>
      <div>
        <dt class="font-semibold text-slate-800">Ubicacion</dt>
        <dd class="mt-1 text-slate-600">{location || 'No registrada'}</dd>
      </div>
      <div>
        <dt class="font-semibold text-slate-800">Firma del waiver</dt>
        <dd class="mt-1 text-slate-600">{formatDate(customer.liabilityWaiverSignedAt)}</dd>
      </div>
    </dl>
  {:else}
    <div>
      <p class="text-sm font-semibold text-slate-900">No hay cuenta cargada</p>
      <p class="mt-1 text-sm text-slate-600">
        {licenseNumber
          ? `No encontramos informacion para la licencia ${licenseNumber}.`
          : 'Busca tu licencia en Reserva para cargar tu informacion antes de completar la reserva.'}
      </p>
    </div>
  {/if}
</aside>

{#if showBookings}
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
    <div class="w-full max-w-3xl rounded-md border border-slate-200 bg-white p-4 shadow-sm">
      <div class="mb-4 flex items-center justify-between">
        <h3 class="text-base font-semibold text-slate-900">Historial de Reservas</h3>
        <button type="button" class="rounded-md px-2 py-1 text-sm text-slate-600 hover:bg-slate-100" on:click={() => (showBookings = false)}>Cerrar</button>
      </div>

      {#if customer?.bookings?.length > 0}
        <div class="max-h-80 overflow-auto rounded-md border border-slate-200">
          <table class="min-w-full divide-y divide-slate-200 text-sm">
            <thead class="bg-slate-50 text-left text-slate-600">
              <tr>
                <th class="px-3 py-2 font-semibold">Fecha</th>
                <th class="px-3 py-2 font-semibold">Inicio</th>
                <th class="px-3 py-2 font-semibold">Termina</th>
                <th class="px-3 py-2 font-semibold">Duracion</th>
                <th class="px-3 py-2 font-semibold">Scooters</th>
                <th class="px-3 py-2 font-semibold">E-bikes</th>
                <th class="px-3 py-2 font-semibold">Estado</th>
                <th class="px-3 py-2 font-semibold">Nota admin</th>
                <th class="px-3 py-2 font-semibold"></th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100">
              {#each customer.bookings as booking}
                <tr>
                  <td class="px-3 py-2 text-slate-700">{new Date(booking.requestedStart).toLocaleDateString('es-PR')}</td>
                  <td class="px-3 py-2 text-slate-700">{new Date(booking.requestedStart).toLocaleTimeString('es-PR', { hour: '2-digit', minute: '2-digit' })}</td>
                  <td class="px-3 py-2 text-slate-700">{booking.requestedEnd ? new Date(booking.requestedEnd).toLocaleTimeString('es-PR', { hour: '2-digit', minute: '2-digit' }) : '-'}</td>
                  <td class="px-3 py-2 text-slate-700">{booking.requestedEnd ? Math.round((new Date(booking.requestedEnd) - new Date(booking.requestedStart)) / 3600000) : '-'} h</td>
                  <td class="px-3 py-2 text-slate-700">{booking.scooterQuantity}</td>
                  <td class="px-3 py-2 text-slate-700">{booking.ebikeQuantity}</td>
                  <td class="px-3 py-2 font-medium text-slate-800">{booking.status}</td>
                  <td class="px-3 py-2 text-slate-600">{booking.adminNotes || '-'}</td>
                  <td class="px-3 py-2 text-right">
                    {#if booking.canDelete}
                      <a class="rounded-md bg-slate-100 px-2 py-1 text-xs font-semibold text-slate-700 hover:bg-slate-200" href={`/Bookings/DeleteOwn?id=${booking.id}&licenseNumber=${encodeURIComponent(customer.licenseNumber || '')}`}>
                        Eliminar
                      </a>
                    {/if}
                    {#if booking.reconfirmRequested}
                      <div class="mt-1 flex gap-1">
                        <a class="rounded-md bg-emerald-100 px-2 py-1 text-xs font-semibold text-emerald-700 hover:bg-emerald-200" href={`/Bookings/RespondReconfirm?id=${booking.id}&accept=true&licenseNumber=${encodeURIComponent(customer.licenseNumber || '')}`}>
                          Aceptar
                        </a>
                        <a class="rounded-md bg-red-100 px-2 py-1 text-xs font-semibold text-red-700 hover:bg-red-200" href={`/Bookings/RespondReconfirm?id=${booking.id}&accept=false&licenseNumber=${encodeURIComponent(customer.licenseNumber || '')}`}>
                          Rechazar
                        </a>
                      </div>
                    {/if}
                  </td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>
      {:else}
        <p class="text-sm text-slate-600">No tienes reservas registradas.</p>
      {/if}
    </div>
  </div>
{/if}
