window.scrollHelpers = {
    enableTimelineScroll: function() {
        // Agregar clase al body para aplicar estilos de scroll unificado
        document.body.classList.add('timeline-page');
        // Desactivar scroll en html y body
        document.documentElement.style.overflow = 'hidden';
        document.documentElement.style.height = '100vh';
        document.body.style.overflow = 'hidden';
        document.body.style.height = '100vh';
    },
    disableTimelineScroll: function() {
        // Remover clase del body al salir de la página
        document.body.classList.remove('timeline-page');
        // Restaurar estilos por defecto
        document.documentElement.style.overflow = '';
        document.documentElement.style.height = '';
        document.body.style.overflow = '';
        document.body.style.height = '';
    },
    getScrollContainer: function() {
        // En la página de Timeline, usar el contenedor del timeline como único scroll
        const timelineContainer = document.getElementById('timeline-scroll-container');
        if (timelineContainer) {
            return timelineContainer;
        }
        
        // Para otras páginas, mantener el comportamiento original
        const mainContent = document.querySelector('.mud-main-content');
        if (mainContent && mainContent.scrollHeight > mainContent.clientHeight) {
            const style = window.getComputedStyle(mainContent);
            if (style.overflowY !== 'hidden' && style.display !== 'none' && style.overflowY !== 'visible') {
                return mainContent;
            }
        }
        
        const layout = document.querySelector('.mud-layout');
        if (layout && layout.scrollHeight > layout.clientHeight) {
            const style = window.getComputedStyle(layout);
            if (style.overflowY !== 'hidden' && style.display !== 'none' && style.overflowY !== 'visible') {
                return layout;
            }
        }

        return window;
    },
    scrollToElement: function (id) {
        const element = document.getElementById(id);
        const scrollContainer = this.getScrollContainer();
        if (element && scrollContainer) {
            // Para el contenedor del timeline, calculamos la posición relativa
            let elementPosition;
            if (scrollContainer === window) {
                // Para window, usamos getBoundingClientRect + scrollY
                const rect = element.getBoundingClientRect();
                const appBarHeight = document.querySelector('.mud-appbar')?.offsetHeight || 0;
                elementPosition = rect.top + window.scrollY - appBarHeight - 10;
            } else {
                // Para contenedores internos (como timeline-container), usamos offsetTop
                // El offsetTop es relativo al padre posicionado
                elementPosition = element.offsetTop - 10;
            }
            
            const offsetPosition = Math.max(0, elementPosition);

            if (scrollContainer === window) {
                window.scrollTo({
                    top: offsetPosition,
                    behavior: 'smooth'
                });
            } else {
                scrollContainer.scrollTo({
                    top: offsetPosition,
                    behavior: 'smooth'
                });
            }
        }
    },
    onWindowScroll: function (dotnetHelper, groupIds) {
        const scrollContainer = this.getScrollContainer();
        
        const handleScroll = () => {
            let activeId = "";
            
            for (const id of groupIds) {
                const element = document.getElementById(id);
                if (element) {
                    const rect = element.getBoundingClientRect();
                    // Para el contenedor del timeline, comparamos con la parte superior del contenedor
                    if (scrollContainer === window) {
                        // Para window, comparamos con la parte superior de la ventana
                        if (rect.top <= 150) { 
                            activeId = id;
                        }
                    } else {
                        // Para contenedores internos, comparamos con la parte superior del contenedor
                        const containerRect = scrollContainer.getBoundingClientRect();
                        const relativeTop = rect.top - containerRect.top;
                        if (relativeTop <= 150) { 
                            activeId = id;
                        }
                    }
                }
            }
            
            // Calcular progreso total del scroll
            let winScroll, height;
            if (scrollContainer === window) {
                // Usar window.scrollY para mejor compatibilidad
                winScroll = window.scrollY || window.pageYOffset || document.documentElement.scrollTop;
                // Calcular altura total del documento menos la altura visible
                height = Math.max(
                    document.documentElement.scrollHeight - document.documentElement.clientHeight,
                    document.body.scrollHeight - document.body.clientHeight
                );
            } else {
                winScroll = scrollContainer.scrollTop;
                height = scrollContainer.scrollHeight - scrollContainer.clientHeight;
            }
            
            const scrolled = height > 0 ? (winScroll / height) * 100 : 0;
            
            // Calcular posición del handle en píxeles
            // El handle se mueve dentro del timeline-nav-wrapper que tiene padding-top: 40px y padding-bottom: 40px
            const navWrapper = document.querySelector('.timeline-nav-wrapper');
            let handleTopPixels = 40; // Posición inicial (padding-top)
            
            if (navWrapper) {
                const navHeight = navWrapper.offsetHeight;
                const paddingTop = 40;
                const paddingBottom = 40;
                const handleHeight = 40;
                const availableHeight = navHeight - paddingTop - paddingBottom - handleHeight;
                // Mapear el porcentaje de scroll (0-100) al área disponible
                handleTopPixels = paddingTop + (scrolled / 100) * availableHeight;
            }
            
            dotnetHelper.invokeMethodAsync('OnScrollUpdated', activeId, scrolled, handleTopPixels);
        };

        if (scrollContainer === window) {
            window.addEventListener('scroll', handleScroll, { passive: true });
        } else {
            scrollContainer.addEventListener('scroll', handleScroll, { passive: true });
        }
        window._timelineScrollHandler = handleScroll;
    },
    initTimelineDrag: function (dotnetHelper, containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;

        let isDragging = false;

        const handleDrag = (e) => {
            if (!isDragging) return;
            
            const scrollContainer = this.getScrollContainer();
            const navWrapper = document.querySelector('.timeline-nav-wrapper');
            if (!navWrapper) return;
            
            const rect = navWrapper.getBoundingClientRect();
            const clientY = e.clientY || (e.touches && e.touches[0].clientY);
            const y = clientY - rect.top;
            
            // Calcular porcentaje considerando los paddings (40px arriba y abajo)
            const paddingTop = 40;
            const paddingBottom = 40;
            const availableHeight = rect.height - paddingTop - paddingBottom;
            const relativeY = y - paddingTop;
            let percentage = relativeY / availableHeight;
            percentage = Math.max(0, Math.min(1, percentage));
            
            if (scrollContainer === window) {
                const scrollHeight = document.documentElement.scrollHeight - document.documentElement.clientHeight;
                window.scrollTo(0, scrollHeight * percentage);
            } else {
                const scrollHeight = scrollContainer.scrollHeight - scrollContainer.clientHeight;
                scrollContainer.scrollTop = scrollHeight * percentage;
            }
        };

        const startDragging = (e) => {
            isDragging = true;
            handleDrag(e);
            document.addEventListener('mousemove', handleDrag);
            document.addEventListener('mouseup', stopDragging);
            document.addEventListener('touchmove', handleDrag);
            document.addEventListener('touchend', stopDragging);
            container.classList.add('dragging');
        };

        const stopDragging = () => {
            isDragging = false;
            document.removeEventListener('mousemove', handleDrag);
            document.removeEventListener('mouseup', stopDragging);
            document.removeEventListener('touchmove', handleDrag);
            document.removeEventListener('touchend', stopDragging);
            container.classList.remove('dragging');
        };

        container.addEventListener('mousedown', startDragging);
        container.addEventListener('touchstart', startDragging);
    }
};

window.videoHelpers = {
    play: function (videoElement) {
        if (videoElement) {
            videoElement.play().catch(error => {
                // Ignore autoplay errors if any
                console.log("Autoplay prevented or video error: ", error);
            });
        }
    },
    pause: function (videoElement) {
        if (videoElement) {
            videoElement.pause();
            // Reset to beginning to show the first frame again
            videoElement.currentTime = 0;
        }
    }
};