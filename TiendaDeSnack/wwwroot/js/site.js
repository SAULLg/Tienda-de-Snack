// Variable global para cargar la lista del carrito (necesario para el alcance)
let loadCartItems;

$(document).ready(function () {
    const offcanvasElement = document.getElementById('offcanvasCarrito');
    const carritoList = $('#carrito-items-list');
    const subtotalDisplay = $('#carrito-subtotal');

    // Cargar y mostrar ítems del carrito
    loadCartItems = function () {
        try {
            console.debug('[Cart] Iniciando carga del carrito...');
            const previousHtml = carritoList.html();

            carritoList.html('<p class="text-center text-muted mt-5"><i class="fas fa-sync-alt fa-spin me-2"></i> Cargando carrito...</p>');
            subtotalDisplay.text('$0.00');

            $.ajax({
                url: '/Home/GetCartItems',
                type: 'GET',
                cache: false,
                success: function (data) {
                    try {
                        carritoList.empty();

                        if (!data || !Array.isArray(data.items) || data.items.length === 0) {
                            carritoList.html('<p class="text-center text-muted mt-5">Tu carrito está vacío. 🥺</p>');
                            subtotalDisplay.text('$0.00');
                            console.debug('[Cart] Carrito vacío o datos incorrectos.', data);
                            return;
                        }

                        data.items.forEach(item => {
                            const nombre = item.Nombre ?? item.nombre ?? 'Producto';
                            const precio = Number(item.Precio ?? item.precio ?? 0);
                            const cantidad = Number(item.Cantidad ?? item.cantidad ?? 0);
                            const id = item.Id ?? item.id ?? '';
                            const subtotal = Number(item.Subtotal ?? item.subtotal ?? precio * cantidad);

                            const itemHtml = `
                                <div class="d-flex align-items-center mb-3 pb-2 border-bottom">
                                    <div class="flex-grow-1">
                                        <h6 class="mb-0">${escapeHtml(nombre)}</h6>
                                        <small class="text-muted">$${precio.toFixed(2)} c/u</small>
                                    </div>
                                    <div class="text-end">
                                        <span class="d-block fw-bold">$${subtotal.toFixed(2)}</span>
                                        <div class="d-flex align-items-center justify-content-end mt-1">
                                            <button class="btn btn-sm btn-outline-secondary me-1 cart-decrement" data-item-id="${id}">-</button>
                                            <span class="mx-1 cart-qty">${cantidad}</span>
                                            <button class="btn btn-sm btn-outline-primary ms-1 cart-increment" data-item-id="${id}">+</button>
                                        </div>
                                    </div>
                                </div>
                            `;

                            carritoList.append(itemHtml);
                        });

                        const subtotalTotal = Number(data.subtotal ?? data.Subtotal ?? 0);
                        subtotalDisplay.text('$' + subtotalTotal.toFixed(2));
                        console.debug('[Cart] Carga completada con éxito.', data);
                    } catch (innerErr) {
                        console.error('[Cart] Error al procesar la respuesta del servidor.', innerErr, data);
                        carritoList.html(previousHtml);
                        subtotalDisplay.text('$0.00');
                    }
                },
                error: function (xhr, status, err) {
                    console.error('[Cart] Error al cargar el carrito:', status, err, xhr);
                    if (previousHtml && previousHtml.trim().length > 0) {
                        carritoList.html(previousHtml);
                    } else {
                        carritoList.html('<p class="text-center text-danger mt-5">Error al cargar el carrito.</p>');
                    }
                    subtotalDisplay.text('$0.00');
                }
            });
        } catch (e) {
            console.error('[Cart] Excepción inesperada al iniciar la carga del carrito:', e);
        }
    };

    // Añadir desde el menú
    $('body').on('click', '.add-to-cart-btn', function (event) {
        event.preventDefault();
        const button = $(this);
        const productId = button.data('product-id');
        if (!productId || button.prop('disabled')) return;

        const originalContent = button.html();
        button.prop('disabled', true).html('<i class="fas fa-sync-alt fa-spin"></i>');

        $.ajax({
            url: '/Home/AddToCart',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ productId: productId }),
            success: function (response) {
                console.debug('[Cart] AddToCart response', response);
                if (response && response.success) {
                    setTimeout(function () { loadCartItems(); try { bootstrap.Offcanvas.getOrCreateInstance(offcanvasElement).show(); } catch(e){} }, 50);
                    button.css('background-color', '#28a745');
                } else {
                    alert('Error: ' + (response && response.message ? response.message : 'No se pudo añadir el producto.'));
                    button.css('background-color', '#dc3545');
                }
            },
            error: function (xhr, status, err) { console.error('[Cart] Error AddToCart', status, err, xhr); alert('Error de conexión al servidor.'); button.css('background-color', '#dc3545'); },
            complete: function () { setTimeout(function () { button.html(originalContent).css('background-color', '#f7d23a').prop('disabled', false); }, 500); }
        });
    });

    // Incrementar cantidad
    $('body').on('click', '.cart-increment', function () {
        const btn = $(this);
        const itemId = btn.data('item-id');
        console.debug('[Cart] Increment clicked for', itemId);
        if (!itemId) { console.warn('[Cart] itemId vacío en increment'); return; }
        btn.prop('disabled', true);

        $.ajax({
            url: '/Home/IncrementCartItem',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ itemId: itemId }),
            success: function (resp) { console.debug('[Cart] Increment response', resp); if (resp && resp.success) loadCartItems(); else alert('No se pudo incrementar el item.'); },
            error: function (xhr, status, err) { console.error('[Cart] Error increment:', status, err, xhr); alert('Error al incrementar el item.'); },
            complete: function () { btn.prop('disabled', false); }
        });
    });

    // Decrementar cantidad
    $('body').on('click', '.cart-decrement', function () {
        const btn = $(this);
        const itemId = btn.data('item-id');
        console.debug('[Cart] Decrement clicked for', itemId);
        if (!itemId) { console.warn('[Cart] itemId vacío en decrement'); return; }
        btn.prop('disabled', true);

        $.ajax({
            url: '/Home/DecrementCartItem',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ itemId: itemId }),
            success: function (resp) { console.debug('[Cart] Decrement response', resp); if (resp && resp.success) loadCartItems(); else alert('No se pudo decrementar el item.'); },
            error: function (xhr, status, err) { console.error('[Cart] Error decrement:', status, err, xhr); alert('Error al decrementar el item.'); },
            complete: function () { btn.prop('disabled', false); }
        });
    });

    // Abrir offcanvas carga el carrito
    if (offcanvasElement) offcanvasElement.addEventListener('show.bs.offcanvas', loadCartItems);

    function escapeHtml(unsafe) {
        return String(unsafe)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }
});