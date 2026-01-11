window.mapHelpers = {
    _mapInstance: null,
    
    initMap: function (elementId, centerLat, centerLng, zoom) {
        if (typeof L === 'undefined') {
            console.error('Leaflet library not loaded');
            return null;
        }
        
        const element = document.getElementById(elementId);
        if (!element) {
            console.error(`Element with id '${elementId}' not found`);
            return null;
        }
        
        try {
            const map = L.map(elementId, {
                center: [centerLat, centerLng],
                zoom: zoom,
                zoomControl: true, // Controles de zoom por defecto
                attributionControl: true // Control de atribución
            });
            
            // Capa de OpenStreetMap
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
                maxZoom: 19
            }).addTo(map);
            
            // Agregar control de escala (abajo a la izquierda)
            L.control.scale({
                position: 'bottomleft',
                metric: true,
                imperial: false
            }).addTo(map);
            
            // Agregar control de ubicación actual (si está disponible)
            // Nota: L.control.locate requiere el plugin leaflet-locatecontrol
            // Por ahora, creamos un control simple manualmente
            if (navigator.geolocation) {
                const locateControl = L.control({
                    position: 'topleft'
                });
                
                locateControl.onAdd = function() {
                    const container = L.DomUtil.create('div', 'leaflet-bar leaflet-control');
                    const button = L.DomUtil.create('a', 'leaflet-control-locate', container);
                    button.href = '#';
                    button.title = 'Mostrar mi ubicación';
                    button.innerHTML = '📍';
                    button.style.cssText = 'line-height: 30px; text-align: center; font-size: 16px; width: 30px; height: 30px; display: block;';
                    
                    L.DomEvent.disableClickPropagation(button);
                    L.DomEvent.on(button, 'click', function(e) {
                        L.DomEvent.stopPropagation(e);
                        L.DomEvent.preventDefault(e);
                        
                        navigator.geolocation.getCurrentPosition(function(position) {
                            map.setView([position.coords.latitude, position.coords.longitude], 13);
                        }, function(error) {
                            console.error('Error getting location:', error);
                            alert('No se pudo obtener tu ubicación');
                        });
                    });
                    
                    return container;
                };
                
                locateControl.addTo(map);
            }
            
            // Agregar control de pantalla completa
            const fullscreenControl = L.control({
                position: 'topleft'
            });
            
            fullscreenControl.onAdd = function() {
                const container = L.DomUtil.create('div', 'leaflet-bar leaflet-control');
                const button = L.DomUtil.create('a', 'leaflet-control-fullscreen', container);
                button.href = '#';
                button.title = 'Pantalla completa';
                button.innerHTML = '⛶';
                button.style.cssText = 'line-height: 30px; text-align: center; font-size: 18px; width: 30px; height: 30px; display: block;';
                
                L.DomEvent.disableClickPropagation(button);
                L.DomEvent.on(button, 'click', function(e) {
                    L.DomEvent.stopPropagation(e);
                    L.DomEvent.preventDefault(e);
                    
                    const isFullscreen = document.fullscreenElement || 
                                        document.webkitFullscreenElement || 
                                        document.mozFullScreenElement || 
                                        document.msFullscreenElement;
                    
                    if (!isFullscreen) {
                        const element = document.documentElement;
                        if (element.requestFullscreen) {
                            element.requestFullscreen();
                        } else if (element.webkitRequestFullscreen) {
                            element.webkitRequestFullscreen();
                        } else if (element.mozRequestFullScreen) {
                            element.mozRequestFullScreen();
                        } else if (element.msRequestFullscreen) {
                            element.msRequestFullscreen();
                        }
                    } else {
                        if (document.exitFullscreen) {
                            document.exitFullscreen();
                        } else if (document.webkitExitFullscreen) {
                            document.webkitExitFullscreen();
                        } else if (document.mozCancelFullScreen) {
                            document.mozCancelFullScreen();
                        } else if (document.msExitFullscreen) {
                            document.msExitFullscreen();
                        }
                    }
                });
                
                return container;
            };
            
            fullscreenControl.addTo(map);
            
            window.mapHelpers._mapInstance = map;
            console.log('Map initialized successfully');
            return map;
        } catch (error) {
            console.error('Error initializing map:', error);
            return null;
        }
    },
    
    getMap: function () {
        return window.mapHelpers._mapInstance;
    },
    
    addClusterMarker: function (lat, lng, count, thumbnailUrl, dotNetRef, clusterIndex) {
        console.log(`addClusterMarker called: lat=${lat}, lng=${lng}, count=${count}, thumbnailUrl=${thumbnailUrl}, clusterIndex=${clusterIndex}`);
        
        const map = window.mapHelpers._mapInstance;
        if (!map) {
            console.error('Map instance not found');
            return null;
        }
        
        if (typeof L === 'undefined') {
            console.error('Leaflet not loaded');
            return null;
        }
        
        // Círculos más pequeños: mínimo 40px, máximo 80px
        const radius = Math.max(40, Math.min(80, 35 + count * 2));
        const size = radius * 2;
        
        console.log(`Creating marker with radius=${radius}, size=${size}`);
        
        // Crear el contenido del marcador con la miniatura - diseño flat
        let htmlContent = '';
        if (thumbnailUrl && thumbnailUrl.trim() !== '') {
            htmlContent = `
                <div style="
                    width: ${size}px;
                    height: ${size}px;
                    border-radius: 50%;
                    overflow: visible;
                    border: 2px solid #1976d2;
                    position: relative;
                    background: #1976d2;
                ">
                    <img src="${thumbnailUrl}" 
                         alt="Cluster ${count}" 
                         style="
                             width: 100%;
                             height: 100%;
                             object-fit: cover;
                             border-radius: 50%;
                         "
                         onerror="this.style.display='none'; this.parentElement.innerHTML='<div style=\\'width: ${size}px; height: ${size}px; display: flex; align-items: center; justify-content: center; color: white; font-weight: bold; font-size: 14px; background: #1976d2; border-radius: 50%; border: 2px solid #1976d2; position: relative;\\'><div style=\\'position: absolute; top: -8px; right: -8px; background: #ff5722; color: white; font-weight: bold; font-size: 12px; padding: 3px 7px; border-radius: 12px; min-width: 22px; text-align: center; border: 2px solid white; line-height: 1.2; z-index: 1000;\\'>${count}</div></div>';"
                    />
                    <div style="
                        position: absolute;
                        top: -8px;
                        right: -8px;
                        background: #ff5722;
                        color: white;
                        font-weight: bold;
                        font-size: 12px;
                        padding: 3px 7px;
                        border-radius: 12px;
                        min-width: 22px;
                        text-align: center;
                        border: 2px solid white;
                        line-height: 1.2;
                        z-index: 1000;
                        box-shadow: 0 1px 3px rgba(0,0,0,0.2);
                    ">${count}</div>
                </div>
            `;
        } else {
            // Si no hay miniatura, mostrar solo el número en un círculo flat con badge
            htmlContent = `
                <div style="
                    width: ${size}px;
                    height: ${size}px;
                    border-radius: 50%;
                    background: #1976d2;
                    border: 2px solid #1976d2;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    color: white;
                    font-weight: bold;
                    font-size: 14px;
                    position: relative;
                ">
                    <div style="
                        position: absolute;
                        top: -8px;
                        right: -8px;
                        background: #ff5722;
                        color: white;
                        font-weight: bold;
                        font-size: 12px;
                        padding: 3px 7px;
                        border-radius: 12px;
                        min-width: 22px;
                        text-align: center;
                        border: 2px solid white;
                        line-height: 1.2;
                        z-index: 1000;
                        box-shadow: 0 1px 3px rgba(0,0,0,0.2);
                    ">${count}</div>
                </div>
            `;
        }
        
        const icon = L.divIcon({
            className: 'map-cluster-icon',
            html: htmlContent,
            iconSize: [size, size],
            iconAnchor: [size / 2, size / 2]
        });
        
        const marker = L.marker([lat, lng], { icon: icon }).addTo(map);
        
        console.log(`Marker added to map at [${lat}, ${lng}]`);
        
        marker.on('click', function() {
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('OnClusterClick', clusterIndex).catch(err => console.error('Error calling OnClusterClick:', err));
            }
        });
        
        return { marker };
    },
    
    fitBounds: function (minLat, minLng, maxLat, maxLng) {
        const map = window.mapHelpers._mapInstance;
        if (!map) return;
        map.fitBounds([[minLat, minLng], [maxLat, maxLng]], {
            padding: [50, 50]
        });
    },
    
    onMapMoveEnd: function (dotNetRef) {
        const map = window.mapHelpers._mapInstance;
        if (!map) return;
        
        map.on('moveend', function() {
            const bounds = map.getBounds();
            const zoom = map.getZoom();
            dotNetRef.invokeMethodAsync('OnMapMoveEnd', 
                bounds.getSouth(),
                bounds.getWest(),
                bounds.getNorth(),
                bounds.getEast(),
                zoom
            );
        });
    },
    
    getMapBounds: function () {
        const map = window.mapHelpers._mapInstance;
        if (!map) return null;
        const bounds = map.getBounds();
        return {
            minLat: bounds.getSouth(),
            minLng: bounds.getWest(),
            maxLat: bounds.getNorth(),
            maxLng: bounds.getEast()
        };
    },
    
    getZoom: function () {
        const map = window.mapHelpers._mapInstance;
        if (!map) return 2;
        return map.getZoom();
    },
    
    removeAllMarkers: function (markers) {
        markers.forEach(m => {
            if (m.marker) m.marker.remove();
            if (m.circle) m.circle.remove();
        });
    }
};
