<script>
  import { onMount } from 'svelte';

  export let formId = 'customer-intake-form';

  let form;
  let values = {};
  let completion = 0;
  let codePreview = 'CLIENT-0000';
  let waiverSigned = false;
  let signatureRequired = false;

  const requiredFields = ['FirstName', 'LastName', 'PhoneNumber', 'City', 'Country'];

  function readForm() {
    if (!form) {
      return;
    }

    values = Object.fromEntries(new FormData(form).entries());
    const waiverInput = form.querySelector('input[name="LiabilityWaiverSigned"][type="checkbox"]');
    waiverSigned = waiverInput?.checked ?? false;
    signatureRequired = waiverSigned;

    const filledRequiredCount = requiredFields.filter((field) => String(values[field] || '').trim().length > 0).length;
    const signatureComplete = !waiverSigned || String(values.ElectronicSignature || '').trim().length > 0;
    completion = Math.round(((filledRequiredCount + (signatureComplete ? 1 : 0)) / (requiredFields.length + 1)) * 100);
    codePreview = buildCodePreview(values.FirstName, values.LastName);

    const signatureInput = form.querySelector('input[name="ElectronicSignature"]');
    if (signatureInput) {
      // Keep validation server-side to avoid browser popups.
      signatureInput.required = false;
      signatureInput.disabled = !waiverSigned;
      signatureInput.closest('[data-signature-field]')?.classList.toggle('opacity-50', !waiverSigned);
    }
  }

  function buildCodePreview(firstName, lastName) {
    const source = String(firstName || lastName || 'CLIENT')
      .replace(/[^a-z0-9]/gi, '')
      .slice(0, 6)
      .toUpperCase();

    return `${source || 'CLIENT'}-####`;
  }

  function formatPhone(event) {
    event.currentTarget.value = event.currentTarget.value.replace(/[^\d+\-().\s]/g, '');
    readForm();
  }

  onMount(() => {
    form = document.getElementById(formId);
    if (!form) {
      return;
    }

    form.addEventListener('input', readForm);
    form.addEventListener('change', readForm);
    form.querySelector('input[name="PhoneNumber"]')?.addEventListener('input', formatPhone);
    readForm();

    return () => {
      form.removeEventListener('input', readForm);
      form.removeEventListener('change', readForm);
      form.querySelector('input[name="PhoneNumber"]')?.removeEventListener('input', formatPhone);
    };
  });
</script>

<aside class="mb-6 grid gap-4 md:grid-cols-[1fr_auto]">
  <!-- <div class="rounded-md border border-slate-200 bg-slate-50 px-4 py-4">
    <div class="flex items-center justify-between gap-4">
      <div>
        <p class="text-sm font-semibold text-slate-900">Progreso</p>
        <p class="mt-1 text-sm text-slate-600">Customer code preview: <span class="font-medium text-slate-950">{codePreview}</span></p>
      </div>
      <span class="text-sm font-semibold text-slate-900">{completion}%</span>
    </div>
    <div class="mt-3 h-2 overflow-hidden rounded-full bg-slate-200">
      <div class="h-full rounded-full bg-emerald-500 transition-all" style={`width: ${completion}%`}></div>
    </div>
  </div> -->
  
  <div class="rounded-md border border-slate-200 bg-white px-4 py-4 md:min-w-64">
    <p class="text-sm font-semibold text-slate-900">Estado del Relevo de Responsabilidad</p>
    <p class="mt-1 text-sm text-slate-600">
      {waiverSigned ? 'Se requiere firma antes de guardar.' : 'La firma se desbloquea después de que se marque el relevo de responsabilidad.'}
    </p>
    {#if signatureRequired && !values.ElectronicSignature}
      <p class="mt-2 text-sm font-medium text-amber-700">Esperando firma electrónica...</p>
    {/if}
  </div>
</aside>
