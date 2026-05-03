<script>
    export let products = [];
</script>

<div class="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
    {#each products as product (product.id)}
        <article class="rounded-md bg-white p-6 shadow-sm ring-1 ring-slate-200">
            <h3 class="text-xl font-semibold text-slate-900">{product.name}</h3>
            <p class="mt-2 font-bold text-orange-600">{product.price}</p>
            <span class="mt-4 inline-flex text-sm" class:text-red-600={product.stock < 10} class:text-slate-600={product.stock >= 10}>
                Stock: {product.stock}
            </span>
        </article>
    {/each}
</div>
