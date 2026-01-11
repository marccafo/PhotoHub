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
            
            // Calcular progreso total del scroll (solo para compatibilidad con la firma del método)
            let winScroll, height;
            if (scrollContainer === window) {
                winScroll = window.scrollY || window.pageYOffset || document.documentElement.scrollTop;
                height = Math.max(
                    document.documentElement.scrollHeight - document.documentElement.clientHeight,
                    document.body.scrollHeight - document.body.clientHeight
                );
            } else {
                winScroll = scrollContainer.scrollTop;
                height = scrollContainer.scrollHeight - scrollContainer.clientHeight;
            }
            
            const scrolled = height > 0 ? (winScroll / height) * 100 : 0;
            
            // Ya no necesitamos calcular la posición del handle, pero mantenemos la firma del método
            dotnetHelper.invokeMethodAsync('OnScrollUpdated', activeId, scrolled, 0);
        };

        if (scrollContainer === window) {
            window.addEventListener('scroll', handleScroll, { passive: true });
        } else {
            scrollContainer.addEventListener('scroll', handleScroll, { passive: true });
        }
        window._timelineScrollHandler = handleScroll;
        
        // Ejecutar inmediatamente para actualizar el estado inicial
        handleScroll();
    },
    updateScrollProgress: function (dotnetHelper) {
        const scrollContainer = this.getScrollContainer();
        if (!scrollContainer) return;
        
        let winScroll, height;
        if (scrollContainer === window) {
            winScroll = window.scrollY || window.pageYOffset || document.documentElement.scrollTop;
            height = Math.max(
                document.documentElement.scrollHeight - document.documentElement.clientHeight,
                document.body.scrollHeight - document.body.clientHeight
            );
        } else {
            winScroll = scrollContainer.scrollTop;
            height = scrollContainer.scrollHeight - scrollContainer.clientHeight;
        }
        
        const scrolled = height > 0 ? (winScroll / height) * 100 : 0;
        dotnetHelper.invokeMethodAsync('OnScrollUpdated', '', scrolled, 0);
    },
    setupTimelineHover: function (dotnetHelper, groupIds, groupLabels) {
        window._timelineGroupIds = groupIds;
        window._timelineGroupLabels = groupLabels;
    },
    getTimelineHoverPosition: function (dotnetHelper, clientY) {
        const indicator = document.querySelector('.timeline-scroll-indicator');
        if (!indicator || !window._timelineGroupIds || !window._timelineGroupLabels) return;
        
        const rect = indicator.getBoundingClientRect();
        const relativeY = clientY - rect.top;
        const percentage = Math.max(0, Math.min(100, (1 - (relativeY / rect.height)) * 100));
        
        const scrollContainer = this.getScrollContainer();
        if (!scrollContainer) return;
        
        // Calcular la posición de scroll correspondiente al porcentaje
        const scrollHeight = scrollContainer.scrollHeight - scrollContainer.clientHeight;
        const targetScroll = (percentage / 100) * scrollHeight;
        
        // Encontrar el grupo más cercano a esa posición
        let closestIndex = 0;
        let closestDistance = Infinity;
        
        window._timelineGroupIds.forEach((groupId, index) => {
            const groupElement = document.getElementById(groupId);
            if (!groupElement) return;
            
            let groupTop;
            if (scrollContainer === window) {
                const groupRect = groupElement.getBoundingClientRect();
                groupTop = groupRect.top + window.scrollY;
            } else {
                groupTop = groupElement.offsetTop;
            }
            
            const distance = Math.abs(groupTop - targetScroll);
            if (distance < closestDistance) {
                closestDistance = distance;
                closestIndex = index;
            }
        });
        
        if (closestIndex < window._timelineGroupLabels.length) {
            const dateLabel = window._timelineGroupLabels[closestIndex];
            dotnetHelper.invokeMethodAsync('OnHoverPositionCalculated', percentage, dateLabel);
        }
    },
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