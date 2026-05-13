<script>
  import { onMount } from 'svelte';

  let { formId = 'booking-form', lookupUrl = '/Bookings/Lookup' } = $props();

  let form;
  let licenseInput;
  let firstTime = $state(false);
  let loading = $state(false);
  let customers = $state([]);
  let selectedCustomer = $state(null);
  let message = $state('Entra una licencia para buscar cuentas existentes.');
  let lookupTimer;

  function syncMode() {
    firstTime = form?.querySelector('input[name="IsFirstTime"][value="true"]')?.checked ?? false;
    form?.querySelector('[data-returning-fields]')?.classList.toggle('hidden', firstTime);
    form?.querySelector('[data-booking-fields]')?.classList.toggle('hidden', firstTime);

    const button = form?.querySelector('[data-submit-label]');
    if (button) {
      button.textContent = firstTime ? 'Ir al registro' : 'Solicitar reserva';
    }
  }

  function queueLookup() {
    selectedCustomer = null;
    clearTimeout(lookupTimer);

    const value = licenseInput?.value?.trim() || '';
    if (value.length < 2 || firstTime) {
      customers = [];
      message = firstTime ? 'Te llevaremos al registro antes de reservar.' : 'Entra una licencia para buscar cuentas existentes.';
      return;
    }

    loading = true;
    lookupTimer = setTimeout(() => lookup(value), 250);
  }

  async function lookup(value) {
    try {
      const response = await fetch(`${lookupUrl}?licenseNumber=${encodeURIComponent(value)}`);
      customers = response.ok ? await response.json() : [];
      message = customers.length > 0 ? 'Selecciona tu cuenta para continuar.' : 'No encontramos una cuenta con esa licencia.';
    } finally {
      loading = false;
    }
  }

  function selectCustomer(customer) {
    selectedCustomer = customer;
    customers = [];
    licenseInput.value = customer.licenseNumber || '';
    licenseInput.dispatchEvent(new Event('input', { bubbles: true }));
  }

  onMount(() => {
    form = document.getElementById(formId);
    licenseInput = form?.querySelector('input[name="LicenseNumber"]');

    if (!form || !licenseInput) {
      return;
    }

    form.addEventListener('change', syncMode);
    licenseInput.addEventListener('input', queueLookup);
    syncMode();
    queueLookup();

    return () => {
      form.removeEventListener('change', syncMode);
      licenseInput.removeEventListener('input', queueLookup);
      clearTimeout(lookupTimer);
    };
  });
</script>

<!-- <aside class="mb-6 rounded-md border border-slate-200 bg-slate-50 px-4 py-4">
  <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
    <div>
      <p class="text-sm font-semibold text-slate-900">Busqueda por licencia</p>
      <p class="mt-1 text-sm text-slate-600">{loading ? 'Buscando...' : message}</p>
    </div>
    <span class="rounded-full bg-white px-3 py-1 text-xs font-semibold text-slate-700 ring-1 ring-slate-200">
      {firstTime ? 'Primera visita' : 'Cliente existente'}
    </span>
  </div>

  {#if selectedCustomer}
    <div class="mt-4 rounded-md border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-900">
      Usando cuenta de <strong>{selectedCustomer.firstName} {selectedCustomer.lastName}</strong>
      <span class="text-emerald-700">({selectedCustomer.customerCode})</span>.
    </div>
  {/if}

  {#if customers.length > 0}
    <div class="mt-4 grid gap-2">
      {#each customers as customer}
        <button
          type="button"
          class="rounded-md border border-slate-200 bg-white px-4 py-3 text-left text-sm shadow-sm transition hover:border-slate-400"
          onclick={() => selectCustomer(customer)}>
          <span class="block font-semibold text-slate-900">{customer.firstName} {customer.lastName}</span>
          <span class="mt-1 block text-slate-600">{customer.licenseNumber} - {customer.city}, {customer.country}</span>
        </button>
      {/each}
    </div>
  {/if}
</aside> -->
